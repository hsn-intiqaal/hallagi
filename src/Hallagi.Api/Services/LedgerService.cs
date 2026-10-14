using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

/// <summary>
/// Barber financial ledger + advances + cash handovers.
/// All amounts are precise decimals; entries are append-only with reversals for corrections.
/// </summary>
public class LedgerService(AppDbContext db)
{
    // ============ Ledger ============

    public async Task<object> ListLedgerAsync(CurrentUser user, Guid barberId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.LedgerView))
            throw ApiException.Forbidden("ليس لديك صلاحية لعرض السجل المالي");

        await EnsureBarberVisibleAsync(user, barberId, ct);

        var fromDate = DateOnly.FromDateTime((from ?? DateTime.Today.AddDays(-30)).Date);
        var toDate = DateOnly.FromDateTime((to ?? DateTime.Today).Date);

        var entries = await db.BarberFinancialTransactions.AsNoTracking()
            .Where(t => t.BarberId == barberId && t.TxDate >= fromDate && t.TxDate <= toDate)
            .OrderByDescending(t => t.TxDate).ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Type,
                TypeText = TypeText(t.Type, t.Status),
                t.Direction,
                t.Amount,
                t.TxDate,
                t.Description,
                t.Status,
                t.SourceType,
                t.SourceRef,
                t.CreatedAt
            }).ToListAsync(ct);

        // Balance counts only live entries: reversed originals and their reversal audit rows are both neutral
        var balance = await db.BarberFinancialTransactions.AsNoTracking()
            .Where(t => t.BarberId == barberId
                        && t.Status != "Reversed"
                        && t.Type != BarberTxType.Reversal)
            .SumAsync(t => t.Direction == BarberTxDirection.Credit ? t.Amount : -t.Amount, ct);

        var advances = await db.BarberAdvances.AsNoTracking()
            .Where(a => a.BarberId == barberId && a.Status != "Cancelled")
            .OrderByDescending(a => a.AdvanceDate)
            .Select(a => new
            {
                a.Id,
                a.OriginalAmount,
                outstanding = a.OriginalAmount - a.RepaidAmount,
                a.AdvanceDate,
                a.Reason,
                a.Status,
                StatusText = AdvanceStatusText(a.Status)
            }).ToListAsync(ct);

        return new
        {
            entries,
            balance,
            advances,
            from = fromDate,
            to = toDate
        };
    }

    public async Task<Guid> AddEntryAsync(CurrentUser user, LedgerEntryRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.LedgerView))
            throw ApiException.Forbidden("ليس لديك صلاحية لتسجيل حركات مالية");
        if (req.Amount <= 0) throw ApiException.Bad("invalid_amount", "المبلغ يجب أن يكون أكبر من صفر");

        await EnsureBarberVisibleAsync(user, req.BarberId, ct);

        // Duplicate prevention for source-linked entries
        if (!string.IsNullOrWhiteSpace(req.SourceRef))
        {
            var exists = await db.BarberFinancialTransactions.AsNoTracking()
                .AnyAsync(t => t.OrganizationId == user.OrganizationId && t.SourceRef == req.SourceRef, ct);
            if (exists) throw ApiException.Conflict("duplicate_entry", "هذه الحركة مسجّلة مسبقًا");
        }

        var entry = new BarberFinancialTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BarberId = req.BarberId,
            BranchId = req.BranchId,
            Type = req.Type,
            Direction = req.Direction,
            Amount = req.Amount,
            TxDate = req.TxDate == default ? DateOnly.FromDateTime(DateTime.Today) : req.TxDate,
            Description = req.Description?.Trim(),
            SourceType = "Manual",
            SourceRef = req.SourceRef?.Trim(),
            Status = "Approved",
            CreatedBy = user.ProfileId,
            ApprovedBy = user.ProfileId,
            CreatedAt = DateTime.UtcNow,
            Notes = req.Notes?.Trim()
        };
        db.BarberFinancialTransactions.Add(entry);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "ledger.entry",
            EntityType = nameof(BarberFinancialTransaction),
            EntityId = entry.Id.ToString(),
            Details = $"barber={req.BarberId}; type={req.Type}; amount={req.Amount}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task ReverseAsync(CurrentUser user, Guid entryId, string reason, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.LedgerView))
            throw ApiException.Forbidden("ليس لديك صلاحية بتعديل السجل المالي");
        if (string.IsNullOrWhiteSpace(reason)) throw ApiException.Bad("reason_required", "اكتب سبب التصحيح");

        var entry = await db.BarberFinancialTransactions
            .FirstOrDefaultAsync(t => t.Id == entryId && t.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("الحركة غير موجودة");
        if (entry.Status == "Reversed")
            throw ApiException.Conflict("already_reversed", "الحركة معكوسة مسبقًا");

        var now = DateTime.UtcNow;
        entry.Status = "Reversed";
        entry.ReversedById = user.ProfileId;

        // Correction is a new visible entry, never a silent edit
        db.BarberFinancialTransactions.Add(new BarberFinancialTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = entry.OrganizationId,
            BarberId = entry.BarberId,
            BranchId = entry.BranchId,
            Type = BarberTxType.Reversal,
            Direction = entry.Direction == BarberTxDirection.Credit ? BarberTxDirection.Debit : BarberTxDirection.Credit,
            Amount = entry.Amount,
            TxDate = DateOnly.FromDateTime(DateTime.Today),
            Description = $"تصحيح: {entry.Description ?? TypeText(entry.Type)}",
            SourceType = "Reversal",
            SourceId = entry.Id,
            SourceRef = $"reversal:{entry.Id}",
            Status = "Approved",
            CreatedBy = user.ProfileId,
            ApprovedBy = user.ProfileId,
            CreatedAt = now,
            Notes = reason.Trim()
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "ledger.reversed",
            EntityType = nameof(BarberFinancialTransaction),
            EntityId = entry.Id.ToString(),
            Details = reason.Trim(),
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    // ============ Advances ============

    public async Task<object> ListAdvancesAsync(CurrentUser user, Guid? barberId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.LedgerView))
            throw ApiException.Forbidden("ليس لديك صلاحية لعرض السلف");

        var query = db.BarberAdvances.AsNoTracking()
            .Where(a => a.OrganizationId == user.OrganizationId && a.Status != "Cancelled");
        if (barberId.HasValue)
        {
            await EnsureBarberVisibleAsync(user, barberId.Value, ct);
            query = query.Where(a => a.BarberId == barberId.Value);
        }

        var items = await query.OrderByDescending(a => a.AdvanceDate)
            .Select(a => new
            {
                a.Id,
                a.BarberId,
                BarberName = db.Barbers.Where(b => b.Id == a.BarberId).Select(b => b.FullName).First(),
                a.OriginalAmount,
                Outstanding = a.OriginalAmount - a.RepaidAmount,
                a.AdvanceDate,
                a.Reason,
                a.Status,
                StatusText = AdvanceStatusText(a.Status)
            }).ToListAsync(ct);
        return new { items };
    }

    public async Task<Guid> CreateAdvanceAsync(CurrentUser user, CreateAdvanceRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AdvanceCreate))
            throw ApiException.Forbidden("ليس لديك صلاحية لتسجيل السلف");
        if (req.Amount <= 0) throw ApiException.Bad("invalid_amount", "المبلغ يجب أن يكون أكبر من صفر");

        await EnsureBarberVisibleAsync(user, req.BarberId, ct);

        var advance = new BarberAdvance
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BarberId = req.BarberId,
            BranchId = req.BranchId,
            OriginalAmount = req.Amount,
            RepaidAmount = 0m,
            AdvanceDate = req.AdvanceDate == default ? DateOnly.FromDateTime(DateTime.Today) : req.AdvanceDate,
            Reason = req.Reason?.Trim(),
            Status = "Approved",
            CreatedBy = user.ProfileId,
            ApprovedBy = user.ProfileId,
            CreatedAt = DateTime.UtcNow
        };
        db.BarberAdvances.Add(advance);

        // Ledger entry: money paid out is a debit on the barber (repayment owed)
        db.BarberFinancialTransactions.Add(new BarberFinancialTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BarberId = req.BarberId,
            BranchId = req.BranchId,
            Type = BarberTxType.AdvanceGiven,
            Direction = BarberTxDirection.Debit,
            Amount = req.Amount,
            TxDate = advance.AdvanceDate,
            Description = "سلفة لحلاق",
            SourceType = nameof(BarberAdvance),
            SourceId = advance.Id,
            SourceRef = $"advance:{advance.Id}",
            Status = "Approved",
            CreatedBy = user.ProfileId,
            ApprovedBy = user.ProfileId,
            CreatedAt = DateTime.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "advance.created",
            EntityType = nameof(BarberAdvance),
            EntityId = advance.Id.ToString(),
            Details = $"barber={req.BarberId}; amount={req.Amount}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return advance.Id;
    }

    public async Task RepayAdvanceAsync(CurrentUser user, Guid advanceId, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0) throw ApiException.Bad("invalid_amount", "المبلغ يجب أن يكون أكبر من صفر");

        var advance = await db.BarberAdvances
            .FirstOrDefaultAsync(a => a.Id == advanceId && a.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("السلفة غير موجودة");

        var outstanding = advance.OriginalAmount - advance.RepaidAmount;
        if (amount > outstanding)
            throw ApiException.Bad("too_much", "مبلغ السداد أكبر من المتبقي على السلفة");

        advance.RepaidAmount += amount;
        if (Math.Round(advance.OriginalAmount - advance.RepaidAmount, 2) <= 0)
            advance.Status = "Settled";

        db.BarberFinancialTransactions.Add(new BarberFinancialTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BarberId = advance.BarberId,
            BranchId = advance.BranchId,
            Type = BarberTxType.AdvanceRepaid,
            Direction = BarberTxDirection.Credit,
            Amount = amount,
            TxDate = DateOnly.FromDateTime(DateTime.Today),
            Description = "سداد سلفة",
            SourceType = nameof(BarberAdvance),
            SourceId = advance.Id,
            SourceRef = $"advance-repay:{advance.Id}:{DateTime.UtcNow.Ticks}",
            Status = "Approved",
            CreatedBy = user.ProfileId,
            ApprovedBy = user.ProfileId,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    // ============ Cash handovers ============

    public async Task<object> ListHandoversAsync(CurrentUser user, Guid? branchId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.LedgerView))
            throw ApiException.Forbidden("ليس لديك صلاحية لعرض التسليمات النقدية");

        var allowed = Permissions.AccessesAllBranches(user)
            ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
            : user.BranchIds.AsQueryable();

        var query = db.CashHandovers.AsNoTracking().Where(x => x.OrganizationId == user.OrganizationId && allowed.Contains(x.BranchId));
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        var items = await query.OrderByDescending(x => x.HandoverDate).ThenByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new
            {
                x.Id,
                x.BranchId,
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                FromUserName = db.Profiles.Where(p => p.Id == x.FromUserId).Select(p => p.FullName).First(),
                ToUserName = db.Profiles.Where(p => p.Id == x.ToUserId).Select(p => p.FullName).First(),
                x.Amount,
                x.HandoverDate,
                x.Note,
                x.Status,
                StatusText = HandoverStatusText(x.Status)
            }).ToListAsync(ct);
        return new { items };
    }

    public async Task<Guid> CreateHandoverAsync(CurrentUser user, CreateHandoverRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.HandoverCreate))
            throw ApiException.Forbidden("ليس لديك صلاحية لتسجيل تسليم نقدي");
        if (req.Amount <= 0) throw ApiException.Bad("invalid_amount", "المبلغ يجب أن يكون أكبر من صفر");
        if (!Permissions.CanAccessBranch(user, req.BranchId))
            throw ApiException.Forbidden("لا تملك صلاحية على هذا الفرع");
        if (req.FromUserId == req.ToUserId)
            throw ApiException.Bad("same_user", "لا يمكن التسليم لنفس الشخص");

        var toUser = await db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == req.ToUserId, ct)
            ?? throw ApiException.Bad("invalid_user", "المستخدم المستلم غير موجود");

        var handover = new CashHandover
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = req.BranchId,
            FromUserId = req.FromUserId,
            ToUserId = req.ToUserId,
            BarberId = req.BarberId,
            ShiftId = req.ShiftId,
            Amount = req.Amount,
            HandoverDate = req.HandoverDate == default ? DateOnly.FromDateTime(DateTime.Today) : req.HandoverDate,
            Note = req.Note?.Trim(),
            Status = "Confirmed",
            ConfirmedBy = user.ProfileId,
            ConfirmedAt = DateTime.UtcNow,
            CreatedBy = user.ProfileId,
            CreatedAt = DateTime.UtcNow
        };
        db.CashHandovers.Add(handover);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "handover.created",
            EntityType = nameof(CashHandover),
            EntityId = handover.Id.ToString(),
            Details = $"amount={req.Amount}; from={req.FromUserId}; to={req.ToUserId}",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return handover.Id;
    }

    // ============ helpers ============

    private async Task EnsureBarberVisibleAsync(CurrentUser user, Guid barberId, CancellationToken ct)
    {
        var barber = await db.Barbers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == barberId && b.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("الحلاق غير موجود");

        // Barbers only see their own record; others need branch access or all-branch permission
        if (user.Role == UserRole.Barber && barber.LinkedProfileId != user.ProfileId)
            throw ApiException.Forbidden("لا تملك صلاحية على هذا الحلاق");

        if (user.Role != UserRole.Owner && !Permissions.AccessesAllBranches(user))
        {
            var assigned = await db.BarberBranchAssignments.AsNoTracking()
                .Where(a => a.BarberId == barberId).Select(a => a.BranchId).ToListAsync(ct);
            if (!assigned.Any(b => user.BranchIds.Contains(b)))
                throw ApiException.Forbidden("لا تملك صلاحية على هذا الحلاق");
        }
    }

    public static string TypeText(BarberTxType type, string status = "Approved") => type switch
    {
        BarberTxType.CommissionEarned => "عمولة مستحقة",
        BarberTxType.SalaryAccrued => "راتب مستحق",
        BarberTxType.Bonus => "مكافأة",
        BarberTxType.ChairRentDue => "إيجار كرسي مستحق",
        BarberTxType.ChairRentPaid => "دفع إيجار كرسي",
        BarberTxType.AdvanceGiven => "سلفة مُقدَّمة",
        BarberTxType.AdvanceRepaid => "سداد سلفة",
        BarberTxType.ExpensePaidByBarber => "مصروف دفعه الحلاق",
        BarberTxType.CashFloatAllocated => "عهدة نقدية",
        BarberTxType.CashHandover => "تسليم نقدي",
        BarberTxType.ReimbursementDue => "مبلغ مسترد للحلاق",
        BarberTxType.ApprovedAdjustment => "تعديل معتمد",
        BarberTxType.SettlementPayment => "دفعة تسوية",
        BarberTxType.Reversal => "تصحيح/عكس",
        _ => "حركة"
    } + (status == "Reversed" ? " (معكوسة)" : "");

    public static string AdvanceStatusText(string status) => status switch
    {
        "Approved" => "معتمدة",
        "Settled" => "مسدّدة",
        "Pending" => "بانتظار الموافقة",
        "Cancelled" => "ملغاة",
        _ => status
    };

    public static string HandoverStatusText(string status) => status switch
    {
        "Confirmed" => "مؤكد",
        "Pending" => "بانتظار التأكيد",
        "Rejected" => "مرفوض",
        _ => status
    };
}

public record LedgerEntryRequest(
    Guid BarberId,
    Guid? BranchId,
    BarberTxType Type,
    BarberTxDirection Direction,
    decimal Amount,
    DateOnly TxDate,
    string? Description,
    string? SourceRef,
    string? Notes);

public record CreateAdvanceRequest(Guid BarberId, Guid? BranchId, decimal Amount, DateOnly AdvanceDate, string? Reason);
public record CreateHandoverRequest(Guid BranchId, Guid FromUserId, Guid ToUserId, Guid? BarberId, Guid? ShiftId, decimal Amount, DateOnly HandoverDate, string? Note);
