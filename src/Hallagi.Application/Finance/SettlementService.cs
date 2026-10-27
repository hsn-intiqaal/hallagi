using System.Text.Json;

namespace Hallagi.Application.Finance;

public class SettlementService(IAppDbContext db, OrgClock clock)
{
    /// <summary>Creates a payroll period (e.g. "أكتوبر 2026").</summary>
    public async Task<Guid> CreatePeriodAsync(CurrentUser user, string name, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementApprove))
            throw AppException.Forbidden("ليس لديك صلاحية لإنشاء فترة مالية");
        if (string.IsNullOrWhiteSpace(name)) throw AppException.Bad("name_required", "اسم الفترة مطلوب");
        if (end < start) throw AppException.Bad("bad_range", "تاريخ النهاية قبل تاريخ البداية");

        var exists = await db.PayrollPeriods.AsNoTracking()
            .AnyAsync(p => p.OrganizationId == user.OrganizationId && p.StartDate == start && p.EndDate == end, ct);
        if (exists) throw AppException.Conflict("period_exists", "توجد فترة مالية بنفس التواريخ");

        var period = new PayrollPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            Name = name.Trim(),
            StartDate = start,
            EndDate = end,
            Status = "Open",
            CreatedBy = user.ProfileId,
            CreatedAt = DateTime.UtcNow
        };
        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return period.Id;
    }

    public async Task<object> ListPeriodsAsync(CurrentUser user, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementPreview))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض التسويات");

        var items = await db.PayrollPeriods.AsNoTracking()
            .Where(p => p.OrganizationId == user.OrganizationId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.StartDate,
                p.EndDate,
                p.Status,
                settlementsCount = db.BarberSettlements.Count(s => s.PayrollPeriodId == p.Id),
                draftCount = db.BarberSettlements.Count(s => s.PayrollPeriodId == p.Id && s.Status == "Draft"),
                approvedCount = db.BarberSettlements.Count(s => s.PayrollPeriodId == p.Id && s.Status == "Approved"),
                paidCount = db.BarberSettlements.Count(s => s.PayrollPeriodId == p.Id && s.Status == "Paid"),
                totalNet = db.BarberSettlements.Where(s => s.PayrollPeriodId == p.Id && s.Status != "Cancelled").Sum(s => s.NetPayable)
            }).ToListAsync(ct);
        return new { items };
    }

    /// <summary>
    /// Preview: creates/refreshes DRAFT settlements for the given barbers in the period.
    /// Calculations depend on each barber's active agreement at the calculation date.
    /// </summary>
    public async Task<object> PreviewAsync(CurrentUser user, Guid periodId, List<Guid>? barberIds, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementPreview))
            throw AppException.Forbidden("ليس لديك صلاحية لمعاينة التسويات");

        var period = await db.PayrollPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId && p.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الفترة المالية غير موجودة");
        if (period.Status != "Open")
            throw AppException.Conflict("period_closed", "الفترة المالية مغلقة");

        // the period's local calendar days → exact UTC window of sales that belong to it
        var window = DateRange.Create(period.StartDate, period.EndDate, await clock.ZoneAsync(user.OrganizationId, ct));
        var from = window.FromUtc;
        var to = window.ToUtc.AddTicks(-1);

        var allBarbers = await db.Barbers.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active)
            .ToListAsync(ct);
        var barbers = barberIds is { Count: > 0 } ? allBarbers.Where(b => barberIds.Contains(b.Id)).ToList() : allBarbers;

        var results = new List<object>();
        foreach (var barber in barbers)
        {
            // Only owner or users with access to the barber's branch may preview
            if (user.Role != UserRole.Owner && !Permissions.AccessesAllBranches(user))
            {
                var assigned = await db.BarberBranchAssignments.AsNoTracking()
                    .Where(a => a.BarberId == barber.Id).Select(a => a.BranchId).ToListAsync(ct);
                if (!assigned.Any(b => user.BranchIds.Contains(b))) continue;
            }

            var calc = await CalculateAsync(user.OrganizationId, barber.Id, from, to, period.EndDate, ct);
            var settlement = await UpsertDraftAsync(user, period, barber, calc, ct);
            await db.SaveChangesAsync(ct); // persist draft before projecting
            results.Add(await ProjectAsync(settlement.Id, ct));
        }

        return new { period = new { period.Id, period.Name, period.StartDate, period.EndDate }, settlements = results };
    }

    public async Task<object> GetAsync(CurrentUser user, Guid settlementId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementPreview))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض التسويات");
        return await ProjectAsync(settlementId, ct);
    }

    public async Task<object> ListAsync(CurrentUser user, Guid? periodId, string? status, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementPreview))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض التسويات");

        var query = db.BarberSettlements.AsNoTracking().Where(s => s.OrganizationId == user.OrganizationId);
        if (periodId.HasValue) query = query.Where(s => s.PayrollPeriodId == periodId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status);

        var items = await query.OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.PayrollPeriodId,
                PeriodName = db.PayrollPeriods.Where(p => p.Id == s.PayrollPeriodId).Select(p => p.Name).First(),
                s.BarberId,
                BarberName = db.Barbers.Where(b => b.Id == s.BarberId).Select(b => b.FullName).First(),
                s.Status,
                StatusText = StatusText(s.Status),
                s.SalaryEarned,
                s.CommissionEarned,
                s.Bonus,
                s.Reimbursements,
                s.Deductions,
                s.AdvancesRepaid,
                s.ChairRent,
                s.NetPayable,
                s.AlreadyPaid,
                Remaining = s.NetPayable - s.AlreadyPaid,
                s.ApprovedAt
            }).ToListAsync(ct);
        return new { items };
    }

    /// <summary>Approves and locks a draft settlement; posts earned amounts to the ledger once.</summary>
    public async Task ApproveAsync(CurrentUser user, Guid settlementId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementApprove))
            throw AppException.Forbidden("ليس لديك صلاحية لاعتماد التسويات");

        var settlement = await db.BarberSettlements
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == settlementId && s.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("التسوية غير موجودة");
        if (settlement.Status != "Draft")
            throw AppException.Conflict("not_draft", "التسوية معتمدة أو مدفوعة مسبقًا");

        var now = DateTime.UtcNow;
        settlement.Status = "Approved";
        settlement.ApprovedBy = user.ProfileId;
        settlement.ApprovedAt = now;
        settlement.UpdatedAt = now;

        await PostEarnedLedgerAsync(user, settlement, now, ct);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "settlement.approved",
            EntityType = nameof(BarberSettlement),
            EntityId = settlement.Id.ToString(),
            Details = $"net={settlement.NetPayable}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(CurrentUser user, Guid settlementId, string reason, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementApprove))
            throw AppException.Forbidden("ليس لديك صلاحية بإلغاء التسويات");
        if (string.IsNullOrWhiteSpace(reason)) throw AppException.Bad("reason_required", "اكتب سبب الإلغاء");

        var settlement = await db.BarberSettlements
            .FirstOrDefaultAsync(s => s.Id == settlementId && s.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("التسوية غير موجودة");
        if (settlement.Status == "Paid")
            throw AppException.Conflict("already_paid", "لا يمكن إلغاء تسوية مدفوعة — استخدم تصحيحًا موثقًا");
        if (settlement.Status == "Cancelled")
            throw AppException.Conflict("already_cancelled", "التسوية ملغاة مسبقًا");

        settlement.Status = "Cancelled";
        settlement.Note = reason.Trim();
        settlement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Records a partial payment. Payments never mutate the approved calculation.</summary>
    public async Task PayAsync(CurrentUser user, Guid settlementId, decimal amount, string? method, DateOnly? paymentDate, string? note, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettlementPay))
            throw AppException.Forbidden("ليس لديك صلاحية لتسجيل دفعات التسويات");
        if (amount <= 0) throw AppException.Bad("invalid_amount", "المبلغ يجب أن يكون أكبر من صفر");

        var settlement = await db.BarberSettlements
            .FirstOrDefaultAsync(s => s.Id == settlementId && s.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("التسوية غير موجودة");
        if (settlement.Status is not ("Approved" or "Paid"))
            throw AppException.Conflict("not_approved", "لا يمكن الدفع قبل اعتماد التسوية");

        var remaining = settlement.NetPayable - settlement.AlreadyPaid;
        if (amount > remaining)
            throw AppException.Bad("too_much", "المبلغ أكبر من المتبقي على التسوية");

        var now = DateTime.UtcNow;
        var payment = new SettlementPayment
        {
            Id = Guid.NewGuid(),
            SettlementId = settlement.Id,
            Amount = amount,
            PaymentDate = paymentDate ?? await clock.TodayAsync(user.OrganizationId, ct),
            Method = string.IsNullOrWhiteSpace(method) ? "Cash" : method.Trim(),
            Note = note?.Trim(),
            CreatedBy = user.ProfileId,
            CreatedAt = now
        };
        db.SettlementPayments.Add(payment);

        settlement.AlreadyPaid += amount;
        settlement.Status = settlement.AlreadyPaid >= settlement.NetPayable ? "Paid" : "Approved";
        settlement.UpdatedAt = now;

        // Ledger: the actual payment is what settles the barber's dues
        db.BarberFinancialTransactions.Add(new BarberFinancialTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BarberId = settlement.BarberId,
            BranchId = settlement.BranchId,
            Type = BarberTxType.SettlementPayment,
            Direction = BarberTxDirection.Debit,
            Amount = amount,
            TxDate = payment.PaymentDate,
            Description = "دفعة تسوية",
            SourceType = nameof(BarberSettlement),
            SourceId = settlement.Id,
            SourceRef = $"settlement-pay:{payment.Id}",
            Status = "Approved",
            CreatedBy = user.ProfileId,
            ApprovedBy = user.ProfileId,
            CreatedAt = now
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "settlement.paid",
            EntityType = nameof(BarberSettlement),
            EntityId = settlement.Id.ToString(),
            Details = $"amount={amount}; paid={settlement.AlreadyPaid}/{settlement.NetPayable}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    // ============ calculation ============

    private async Task<SettlementCalc> CalculateAsync(Guid orgId, Guid barberId, DateTime from, DateTime to, DateOnly periodEnd, CancellationToken ct)
    {
        var comp = await db.BarberCompensations.AsNoTracking()
            .Where(c => c.BarberId == barberId && c.Status == EntityStatus.Active && c.EffectiveFrom <= periodEnd)
            .OrderByDescending(c => c.EffectiveFrom).FirstOrDefaultAsync(ct);

        // eligible completed sales in the period
        var eligible = await db.Sales.AsNoTracking()
            .Where(s => s.BarberId == barberId && s.OrganizationId == orgId
                        && s.Status == SaleStatus.Completed && s.CreatedAt >= from && s.CreatedAt <= to)
            .SumAsync(s => (decimal?)s.CommissionBase, ct) ?? 0m;

        var now = DateTime.UtcNow;
        var salary = 0m;
        var commission = 0m;
        var chairRent = 0m;
        var bonus = 0m;
        var explanation = "";

        if (comp is not null)
        {
            switch (comp.Type)
            {
                case CompensationType.FixedSalary:
                    salary = comp.MonthlySalary ?? 0m;
                    explanation = $"راتب شهري ثابت بقيمة اتفاقية مستقلة عن المبيعات (المبيعات المؤهلة {eligible:0.00}).";
                    break;
                case CompensationType.Commission:
                    commission = comp.CommissionRate.HasValue ? Math.Round(eligible * comp.CommissionRate.Value, 2) : 0m;
                    explanation = $"مبيعات مؤهلة {eligible:0.00} × العمولة {comp.CommissionRate:P0} = {commission:0.00}.";
                    break;
                case CompensationType.FixedPlusCommission:
                    salary = comp.MonthlySalary ?? 0m;
                    commission = comp.CommissionRate.HasValue ? Math.Round(eligible * comp.CommissionRate.Value, 2) : 0m;
                    explanation = $"راتب {salary:0.00} + عمولة {comp.CommissionRate:P0} على {eligible:0.00} = {commission:0.00}.";
                    break;
                case CompensationType.ChairRental:
                    chairRent = comp.ChairRentAmount ?? 0m;
                    explanation = $"إيجار كرسي {chairRent:0.00} يُخصم من إيرادات الحلاق (مبيعات {eligible:0.00}) — اتفاق مستقل عن الراتب.";
                    break;
            }
        }

        // advances outstanding before the period end (deducted according to configuration)
        var advancesGiven = await db.BarberAdvances.AsNoTracking()
            .Where(a => a.BarberId == barberId && a.Status != "Cancelled" && a.AdvanceDate <= DateOnly.FromDateTime(to))
            .SumAsync(a => a.OriginalAmount, ct);
        var advancesRepaid = await db.BarberAdvances.AsNoTracking()
            .Where(a => a.BarberId == barberId && a.Status != "Cancelled" && a.AdvanceDate <= DateOnly.FromDateTime(to))
            .SumAsync(a => a.RepaidAmount, ct);

        // reimbursements and approved adjustments from the ledger
        var reimbursements = await db.BarberFinancialTransactions.AsNoTracking()
            .Where(t => t.BarberId == barberId && t.Status != "Reversed" && t.Type != BarberTxType.Reversal
                        && (t.Type == BarberTxType.ReimbursementDue || t.Type == BarberTxType.ApprovedAllowance)
                        && t.TxDate >= DateOnly.FromDateTime(from) && t.TxDate <= DateOnly.FromDateTime(to))
            .SumAsync(t => t.Amount, ct);

        var deductions = await db.BarberFinancialTransactions.AsNoTracking()
            .Where(t => t.BarberId == barberId && t.Status != "Reversed" && t.Type != BarberTxType.Reversal
                        && (t.Type == BarberTxType.ExpensePaidByBarber)
                        && t.TxDate >= DateOnly.FromDateTime(from) && t.TxDate <= DateOnly.FromDateTime(to))
            .SumAsync(t => t.Amount, ct);

        var advancesBalance = advancesGiven - advancesRepaid;

        // Net payable = earned - deductions + reimbursements
        var net = salary + commission + bonus + reimbursements - deductions - advancesBalance;
        if (comp?.Type == CompensationType.ChairRental && chairRent > 0)
            net -= chairRent; // chair renter owes the shop rent

        return new SettlementCalc(
            comp, eligible, salary, commission, bonus, chairRent, reimbursements,
            deductions, advancesBalance, net, explanation,
            JsonSerializer.Serialize(new
            {
                type = comp?.Type.ToString(),
                monthlySalary = comp?.MonthlySalary,
                commissionRate = comp?.CommissionRate,
                chairRentAmount = comp?.ChairRentAmount,
                notes = comp?.Notes,
                effectiveFrom = comp?.EffectiveFrom
            }));
    }

    private sealed record SettlementCalc(
        BarberCompensation? Comp, decimal Eligible, decimal Salary, decimal Commission,
        decimal Bonus, decimal ChairRent, decimal Reimbursements, decimal Deductions,
        decimal AdvancesBalance, decimal Net, string Explanation, string Snapshot);

    private async Task<BarberSettlement> UpsertDraftAsync(CurrentUser user, PayrollPeriod period, Barber barber, SettlementCalc calc, CancellationToken ct)
    {
        // Never recalculate approved/paid settlements; rebuild drafts from scratch
        var existingStatus = await db.BarberSettlements.AsNoTracking()
            .Where(s => s.PayrollPeriodId == period.Id && s.BarberId == barber.Id)
            .Select(s => (string?)s.Status).FirstOrDefaultAsync(ct);

        if (existingStatus is "Approved" or "Paid" or "Cancelled")
        {
            // Locked settlement: return it untouched (never recalculate)
            return await db.BarberSettlements.AsNoTracking()
                .FirstAsync(s => s.PayrollPeriodId == period.Id && s.BarberId == barber.Id, ct);
        }

        // Rebuild drafts from scratch (cascade removes items)
        await db.BarberSettlements.Where(s => s.PayrollPeriodId == period.Id && s.BarberId == barber.Id)
            .ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        var settlement = new BarberSettlement
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            PayrollPeriodId = period.Id,
            BarberId = barber.Id,
            CreatedBy = user.ProfileId,
            CreatedAt = now
        };
        db.BarberSettlements.Add(settlement);

        settlement.AgreementSnapshot = calc.Snapshot;
        settlement.SalaryEarned = calc.Salary;
        settlement.CommissionEarned = calc.Commission;
        settlement.Bonus = calc.Bonus;
        settlement.Allowances = 0m;
        settlement.Reimbursements = calc.Reimbursements;
        settlement.AdvancesRepaid = calc.AdvancesBalance;
        settlement.ChairRent = calc.ChairRent;
        settlement.Deductions = calc.Deductions;
        settlement.PreviousBalance = 0m;
        settlement.NetPayable = Math.Round(calc.Net, 2);
        settlement.UpdatedAt = now;

        void AddItem(string component, decimal amount, string? refText, string? explain)
            => settlement.Items.Add(new SettlementItem
            {
                Id = Guid.NewGuid(),
                SettlementId = settlement.Id,
                Component = component,
                Amount = amount,
                SourceRef = refText,
                Explanation = explain
            });

        if (calc.Salary != 0) AddItem("راتب", calc.Salary, null, "راتب مستحق للفترة");
        if (calc.Commission != 0) AddItem("عمولة", calc.Commission, null, calc.Explanation);
        if (calc.Bonus != 0) AddItem("مكافأة", calc.Bonus, null, null);
        if (calc.Reimbursements != 0) AddItem("مبالغ مستردة", calc.Reimbursements, null, "من السجل المالي");
        if (calc.Deductions != 0) AddItem("خصم/مصروف منسوب", calc.Deductions, null, "من السجل المالي");
        if (calc.ChairRent != 0) AddItem("إيجار كرسي", calc.ChairRent, null, calc.Explanation);
        if (calc.AdvancesBalance != 0) AddItem("رصيد سلف", calc.AdvancesBalance, null, "السلف المعتمدة خلال الفترات السابقة");

        return settlement;
    }

    /// <summary>Posts the approved earned amounts to the ledger exactly once.</summary>
    private async Task PostEarnedLedgerAsync(CurrentUser user, BarberSettlement s, DateTime now, CancellationToken ct)
    {
        async Task PostOnce(BarberTxType type, decimal amount, string label)
        {
            if (amount <= 0) return;
            var refKey = $"settlement:{s.Id}:{type}";
            var exists = await db.BarberFinancialTransactions.AsNoTracking()
                .AnyAsync(t => t.SourceRef == refKey, ct);
            if (exists) return;

            db.BarberFinancialTransactions.Add(new BarberFinancialTransaction
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BarberId = s.BarberId,
                BranchId = s.BranchId,
                Type = type,
                Direction = BarberTxDirection.Credit,
                Amount = amount,
                TxDate = await clock.TodayAsync(user.OrganizationId, ct),
                Description = label,
                SourceType = nameof(BarberSettlement),
                SourceId = s.Id,
                SourceRef = refKey,
                Status = "Approved",
                CreatedBy = user.ProfileId,
                ApprovedBy = user.ProfileId,
                CreatedAt = now
            });
        }

        await PostOnce(BarberTxType.SalaryAccrued, s.SalaryEarned, "راتب مستحق (تسوية معتمدة)");
        await PostOnce(BarberTxType.CommissionEarned, s.CommissionEarned, "عمولة مستحقة (تسوية معتمدة)");
        await PostOnce(BarberTxType.Bonus, s.Bonus, "مكافأة (تسوية معتمدة)");
    }

    private async Task<object> ProjectAsync(Guid settlementId, CancellationToken ct)
    {
        var s = await db.BarberSettlements.AsNoTracking()
            .FirstAsync(x => x.Id == settlementId, ct);
        var items = await db.SettlementItems.AsNoTracking().Where(i => i.SettlementId == s.Id).ToListAsync(ct);
        var payments = await db.SettlementPayments.AsNoTracking().Where(p => p.SettlementId == s.Id).ToListAsync(ct);

        return new
        {
            s.Id,
            s.PayrollPeriodId,
            s.BarberId,
            BarberName = await db.Barbers.Where(b => b.Id == s.BarberId).Select(b => b.FullName).FirstAsync(ct),
            s.Status,
            StatusText = StatusText(s.Status),
            s.SalaryEarned,
            s.CommissionEarned,
            s.Bonus,
            s.Reimbursements,
            s.Deductions,
            s.AdvancesRepaid,
            s.ChairRent,
            s.PreviousBalance,
            s.AlreadyPaid,
            s.NetPayable,
            Remaining = Math.Round(s.NetPayable - s.AlreadyPaid, 2),
            s.ApprovedAt,
            s.Note,
            agreementSnapshot = s.AgreementSnapshot,
            items = items.Select(i => new { i.Id, i.Component, i.Amount, i.SourceRef, i.Explanation }),
            payments = payments.Select(p => new { p.Id, p.Amount, p.PaymentDate, p.Method, p.Note, p.CreatedAt })
        };
    }

    public static string StatusText(string status) => status switch
    {
        "Draft" => "مسودة",
        "Approved" => "معتمدة",
        "Paid" => "مدفوعة",
        "Cancelled" => "ملغاة",
        _ => status
    };
}
