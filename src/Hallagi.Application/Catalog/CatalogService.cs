namespace Hallagi.Application.Catalog;

public partial class CatalogService(IAppDbContext db, OrgClock clock)
{
    // ================= SERVICES =================
    public async Task<object> ListServicesAsync(CurrentUser user, Guid? branchId, string? search, CancellationToken ct = default)
    {
        var query = db.Services.AsNoTracking().Where(s => s.OrganizationId == user.OrganizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Name.Contains(s));
        }
        var items = await query.OrderBy(x => x.Name).ToListAsync(ct);

        var branchPrices = branchId.HasValue
            ? await db.BranchServicePrices.AsNoTracking().Where(p => p.BranchId == branchId.Value)
                .ToDictionaryAsync(p => p.ServiceId, p => p.Price, ct)
            : new Dictionary<Guid, decimal>();

        return new
        {
            items = items.Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.Status,
                StatusText = s.Status == EntityStatus.Active ? "نشطة" : "غير نشطة",
                Price = branchPrices.TryGetValue(s.Id, out var p) ? p : s.BasePrice,
                BasePrice = s.BasePrice,
                s.DurationMinutes,
                HasBranchPrice = branchPrices.ContainsKey(s.Id)
            })
        };
    }

    public async Task<Guid> CreateServiceAsync(CurrentUser user, ServiceInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ServiceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الخدمات");
        if (string.IsNullOrWhiteSpace(input.Name)) throw AppException.Bad("name_required", "اسم الخدمة مطلوب");
        if (input.BasePrice < 0 || input.BasePrice > 100_000) throw AppException.Bad("invalid_price", "السعر غير صحيح");
        if (input.BasePrice == 0 && input.Description is null) { /* free service allowed */ }

        var now = DateTime.UtcNow;
        var service = new Service
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            Name = input.Name.Trim(),
            Description = input.Description?.Trim(),
            BasePrice = input.BasePrice,
            DurationMinutes = NormalizeDuration(input.DurationMinutes, 30),
            Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Services.Add(service);
        db.AuditLogs.Add(Audit("service.created", nameof(Service), service.Id.ToString(), $"name={service.Name}; price={service.BasePrice}", user, now));
        await db.SaveChangesAsync(ct);
        return service.Id;
    }

    public async Task UpdateServiceAsync(CurrentUser user, Guid id, ServiceInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ServiceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الخدمات");
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الخدمة غير موجودة");
        if (string.IsNullOrWhiteSpace(input.Name)) throw AppException.Bad("name_required", "اسم الخدمة مطلوب");
        if (input.BasePrice < 0 || input.BasePrice > 100_000) throw AppException.Bad("invalid_price", "السعر غير صحيح");

        var now = DateTime.UtcNow;
        service.Name = input.Name.Trim();
        service.Description = input.Description?.Trim();
        service.BasePrice = input.BasePrice;
        service.DurationMinutes = NormalizeDuration(input.DurationMinutes, service.DurationMinutes);
        service.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        service.UpdatedAt = now;
        db.AuditLogs.Add(Audit("service.updated", nameof(Service), service.Id.ToString(), $"name={service.Name}; price={service.BasePrice}", user, now));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetServiceStatusAsync(CurrentUser user, Guid id, bool active, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ServiceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الخدمات");
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الخدمة غير موجودة");
        service.Status = active ? EntityStatus.Active : EntityStatus.Inactive;
        service.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetBranchPriceAsync(CurrentUser user, Guid serviceId, Guid branchId, decimal? price, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ServiceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الخدمات");
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == serviceId && s.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الخدمة غير موجودة");
        var existing = await db.BranchServicePrices.FirstOrDefaultAsync(p => p.ServiceId == serviceId && p.BranchId == branchId, ct);
        if (price is null)
        {
            if (existing is not null) db.BranchServicePrices.Remove(existing);
        }
        else
        {
            if (price < 0) throw AppException.Bad("invalid_price", "السعر غير صحيح");
            if (existing is null)
                db.BranchServicePrices.Add(new BranchServicePrice { Id = Guid.NewGuid(), ServiceId = serviceId, BranchId = branchId, Price = price.Value });
            else existing.Price = price.Value;
        }
        await db.SaveChangesAsync(ct);
    }

    private static int NormalizeDuration(int? minutes, int fallback)
    {
        if (minutes is null) return fallback;
        if (minutes is < 5 or > 480) throw AppException.Bad("invalid_duration", "مدة الخدمة بين 5 و480 دقيقة");
        return minutes.Value;
    }

    // ================= BARBERS =================
    public async Task<object> ListBarbersAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var allowed = Permissions.AccessesAllBranches(user)
            ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
            : user.BranchIds.AsQueryable();

        var query = db.Barbers.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId);
        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value)) throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(b => b.BranchAssignments.Any(a => a.BranchId == branchId.Value));
        }

        var barbers = await query.OrderBy(b => b.FullName)
            .Include(b => b.BranchAssignments)
            .ToListAsync(ct);
        // local calendar days → UTC window (inclusive end kept for the existing <= comparisons)
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 30, ct);
        var rangeFrom = range.FromUtc;
        var rangeTo = range.ToUtc.AddTicks(-1);

        var result = new List<object>();
        foreach (var b in barbers)
        {
            var branchIds = b.BranchAssignments.Select(a => a.BranchId).ToList();
            var branchNames = await db.Branches.Where(br => branchIds.Contains(br.Id))
                .Select(br => br.Name).ToListAsync(ct);
            var comp = await db.BarberCompensations.AsNoTracking()
                .Where(c => c.BarberId == b.Id && c.Status == EntityStatus.Active)
                .OrderByDescending(c => c.EffectiveFrom).FirstOrDefaultAsync(ct);
            var salesTotal = await db.Sales.AsNoTracking()
                .Where(s => s.BarberId == b.Id && s.Status == SaleStatus.Completed
                            && s.CreatedAt >= rangeFrom && s.CreatedAt <= rangeTo
                            && (branchId.HasValue || allowed.Contains(s.BranchId)))
                .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
            var salesCount = await db.Sales.AsNoTracking()
                .Where(s => s.BarberId == b.Id && s.Status == SaleStatus.Completed
                            && s.CreatedAt >= rangeFrom && s.CreatedAt <= rangeTo
                            && (branchId.HasValue || allowed.Contains(s.BranchId)))
                .CountAsync(ct);

            result.Add(new
            {
                b.Id,
                b.FullName,
                b.Phone,
                b.Status,
                StatusText = b.Status == EntityStatus.Active ? "نشط" : "غير نشط",
                b.StartDate,
                BranchNames = branchNames,
                SalesTotal = salesTotal,
                SalesCount = salesCount,
                Compensation = comp is null ? null : CompensationText(comp)
            });
        }
        return new { items = result, from = range.From, to = range.To };
    }

    /// <summary>A barber's own profile view: personal data only, compensation visible when permitted.</summary>
    public async Task<object> GetOwnBarberAsync(CurrentUser user, Guid id, bool includeCompensation, CancellationToken ct = default)
    {
        var barber = await db.Barbers.AsNoTracking().Include(b => b.BranchAssignments)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الحلاق غير موجود");

        // A barber can only read his own record
        if (barber.LinkedProfileId != user.ProfileId)
            throw AppException.Forbidden("لا تملك صلاحية على هذا الحلاق");

        var assignedIds = barber.BranchAssignments.Select(a => a.BranchId).ToList();
        var branchNames = await db.Branches.Where(br => assignedIds.Contains(br.Id)).Select(br => br.Name).ToListAsync(ct);

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.BarberId == id && s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt).Take(100)
            .Select(s => new
            {
                s.Id,
                s.ReceiptNo,
                s.CreatedAt,
                s.TotalAmount,
                PaymentText = s.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                ServicesText = string.Join(" + ", s.Items.Select(i => i.ServiceName + (i.Quantity > 1 ? $" ×{i.Quantity}" : ""))),
                CustomerName = s.CustomerId != null
                    ? db.Customers.Where(c => c.Id == s.CustomerId).Select(c => c.FullName).First()
                    : "عميل نقدي",
                BranchName = db.Branches.Where(b => b.Id == s.BranchId).Select(b => b.Name).First()
            }).ToListAsync(ct);

        var summary = await db.Sales.AsNoTracking().Where(s => s.BarberId == id && s.Status == SaleStatus.Completed)
            .GroupBy(s => 1).Select(g => new
            {
                Total = g.Sum(x => x.TotalAmount),
                CommissionBase = g.Sum(x => x.CommissionBase),
                Count = g.Count(),
                ServicesCount = g.SelectMany(x => x.Items).Sum(i => i.Quantity)
            }).FirstOrDefaultAsync(ct);

        object? compensation = null;
        if (includeCompensation)
        {
            var comp = await db.BarberCompensations.AsNoTracking()
                .Where(c => c.BarberId == id && c.Status == EntityStatus.Active)
                .OrderByDescending(c => c.EffectiveFrom).FirstOrDefaultAsync(ct);

            if (comp is not null)
            {
                var commission = comp.CommissionRate.HasValue
                    ? Math.Round((summary?.CommissionBase ?? 0m) * comp.CommissionRate.Value, 2)
                    : 0m;
                compensation = new
                {
                    type = comp.Type.ToString(),
                    typeText = CompensationTypeText(comp.Type),
                    monthlySalary = comp.MonthlySalary,
                    commissionRate = comp.CommissionRate,
                    chairRentAmount = comp.ChairRentAmount,
                    notes = comp.Notes,
                    effectiveFrom = comp.EffectiveFrom.ToString("yyyy-MM-dd"),
                    estimatedCommission = comp.CommissionRate.HasValue ? commission : (decimal?)null,
                    explanation = comp.CommissionRate.HasValue
                        ? $"مبيعات الخدمات المؤهلة (بدون الضريبة والمنتجات): {(summary?.CommissionBase ?? 0m):0.00} ر.س. العمولة: {comp.CommissionRate.Value:P0}. العمولة التقديرية: {commission:0.00} ر.س."
                        : comp.Type == CompensationType.FixedSalary
                            ? "الراتب ثابت بقيمة اتفاقية مستقلة عن المبيعات"
                            : comp.Type == CompensationType.ChairRental
                                ? "اتفاق تأجير كرسي مستقل عن الراتب والعمولة"
                                : null
                };
            }
        }

        return new
        {
            barber = new
            {
                barber.Id,
                barber.FullName,
                barber.Phone,
                barber.StartDate,
                barber.Status,
                statusText = barber.Status == EntityStatus.Active ? "نشط" : "غير نشط",
                branchNames
            },
            compensation,
            summary = new
            {
                total = summary?.Total ?? 0m,
                count = summary?.Count ?? 0,
                servicesCount = summary?.ServicesCount ?? 0
            },
            sales
        };
    }

    public async Task<object> GetBarberAsync(CurrentUser user, Guid id, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var barber = await db.Barbers
            .Include(b => b.BranchAssignments)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الحلاق غير موجود");

        // Barber role may only view his own profile
        if (user.Role == UserRole.Barber && barber.LinkedProfileId != user.ProfileId)
            throw AppException.Forbidden("لا تملك صلاحية على هذا الحلاق");

        // local calendar days → UTC window (inclusive end kept for the existing <= comparisons)
        var range = await clock.RangeAsync(user.OrganizationId, from, to, 30, ct);
        var rangeFrom = range.FromUtc;
        var rangeTo = range.ToUtc.AddTicks(-1);

        var comp = await db.BarberCompensations.AsNoTracking()
            .Where(c => c.BarberId == id && c.Status == EntityStatus.Active)
            .OrderByDescending(c => c.EffectiveFrom).FirstOrDefaultAsync(ct);

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.BarberId == id && s.Status == SaleStatus.Completed
                        && s.CreatedAt >= rangeFrom && s.CreatedAt <= rangeTo)
            .OrderByDescending(s => s.CreatedAt).Take(100)
            .Select(s => new
            {
                s.Id,
                s.ReceiptNo,
                s.CreatedAt,
                s.TotalAmount,
                PaymentText = s.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                ServicesText = string.Join(" + ", s.Items.Select(i => i.ServiceName + (i.Quantity > 1 ? $" ×{i.Quantity}" : ""))),
                CustomerName = s.CustomerId != null
                    ? db.Customers.Where(c => c.Id == s.CustomerId).Select(c => c.FullName).First()
                    : "عميل نقدي",
                BranchName = db.Branches.Where(b => b.Id == s.BranchId).Select(b => b.Name).First()
            }).ToListAsync(ct);

        var summary = await db.Sales.AsNoTracking()
            .Where(s => s.BarberId == id && s.Status == SaleStatus.Completed
                        && s.CreatedAt >= rangeFrom && s.CreatedAt <= rangeTo)
            .GroupBy(s => 1).Select(g => new
            {
                Total = g.Sum(x => x.TotalAmount),
                CommissionBase = g.Sum(x => x.CommissionBase),
                Count = g.Count(),
                ServicesCount = g.SelectMany(x => x.Items).Sum(i => i.Quantity)
            }).FirstOrDefaultAsync(ct);

        var assignedIds = barber.BranchAssignments.Select(a => a.BranchId).ToList();
        return new
        {
            barber = new
            {
                barber.Id,
                barber.FullName,
                barber.Phone,
                barber.StartDate,
                barber.Status,
                StatusText = barber.Status == EntityStatus.Active ? "نشط" : "غير نشط",
                BranchNames = await db.Branches.Where(br => assignedIds.Contains(br.Id)).Select(br => br.Name).ToListAsync(ct),
                BranchIds = assignedIds
            },
            compensation = comp is null ? null : new
            {
                Type = comp.Type.ToString(),
                TypeText = CompensationTypeText(comp.Type),
                comp.MonthlySalary,
                comp.CommissionRate,
                comp.ChairRentAmount,
                comp.Notes,
                comp.EffectiveFrom,
                EstimatedCommission = comp.CommissionRate.HasValue
                    ? Math.Round((summary?.CommissionBase ?? 0m) * comp.CommissionRate.Value, 2) : (decimal?)null,
                Explanation = comp.CommissionRate.HasValue
                    ? $"مبيعات الخدمات المؤهلة (بدون الضريبة والمنتجات): {(summary?.CommissionBase ?? 0m):0.00} ر.س. العمولة: {comp.CommissionRate.Value:P0}. العمولة التقديرية: {Math.Round((summary?.CommissionBase ?? 0m) * comp.CommissionRate.Value, 2):0.00} ر.س."
                    : null
            },
            summary = new
            {
                Total = summary?.Total ?? 0m,
                Count = summary?.Count ?? 0,
                ServicesCount = summary?.ServicesCount ?? 0
            },
            from = range.From,
            to = range.To,
            sales
        };
    }

    public static string CompensationTypeText(CompensationType t) => t switch
    {
        CompensationType.FixedSalary => "راتب ثابت",
        CompensationType.Commission => "عمولة",
        CompensationType.FixedPlusCommission => "راتب ثابت + عمولة",
        _ => "تأجير كرسي"
    };

    public static object CompensationText(BarberCompensation c) => new
    {
        Type = c.Type.ToString(),
        TypeText = CompensationTypeText(c.Type),
        c.MonthlySalary,
        c.CommissionRate,
        c.ChairRentAmount,
        c.Notes,
        c.EffectiveFrom
    };

    public async Task<Guid> CreateBarberAsync(CurrentUser user, BarberInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BarberManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الحلاقين");
        if (string.IsNullOrWhiteSpace(input.FullName)) throw AppException.Bad("name_required", "اسم الحلاق مطلوب");

        var now = DateTime.UtcNow;
        var barber = new Barber
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            FullName = input.FullName.Trim(),
            Phone = input.Phone?.Trim(),
            StartDate = input.StartDate,
            Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Barbers.Add(barber);

        foreach (var br in input.BranchIds.Distinct())
            db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barber.Id, BranchId = br });

        AddCompensation(barber, input.Compensation, now, await clock.TodayAsync(user.OrganizationId, ct));

        db.AuditLogs.Add(Audit("barber.created", nameof(Barber), barber.Id.ToString(), $"name={barber.FullName}", user, now));
        await db.SaveChangesAsync(ct);
        return barber.Id;
    }

    public async Task UpdateBarberAsync(CurrentUser user, Guid id, BarberInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BarberManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة الحلاقين");
        var barber = await db.Barbers.Include(b => b.BranchAssignments).Include(b => b.CompensationSettings)
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الحلاق غير موجود");
        if (string.IsNullOrWhiteSpace(input.FullName)) throw AppException.Bad("name_required", "اسم الحلاق مطلوب");

        var now = DateTime.UtcNow;
        barber.FullName = input.FullName.Trim();
        barber.Phone = input.Phone?.Trim();
        barber.StartDate = input.StartDate;
        barber.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        barber.UpdatedAt = now;

        // Replace branch assignments
        db.BarberBranchAssignments.RemoveRange(barber.BranchAssignments);
        foreach (var br in input.BranchIds.Distinct())
            db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barber.Id, BranchId = br });

        AddCompensation(barber, input.Compensation, now, await clock.TodayAsync(user.OrganizationId, ct));

        db.AuditLogs.Add(Audit("barber.updated", nameof(Barber), barber.Id.ToString(), $"name={barber.FullName}", user, now));
        await db.SaveChangesAsync(ct);
    }

    private void AddCompensation(Barber barber, CompensationInput? input, DateTime now, DateOnly today)
    {
        if (input is null) return;
        var existing = barber.CompensationSettings.Where(c => c.Status == EntityStatus.Active).ToList();
        foreach (var c in existing) c.Status = EntityStatus.Inactive;

        db.BarberCompensations.Add(new BarberCompensation
        {
            Id = Guid.NewGuid(),
            BarberId = barber.Id,
            Type = input.Type,
            MonthlySalary = input.MonthlySalary,
            CommissionRate = input.CommissionRate,
            ChairRentAmount = input.ChairRentAmount,
            Notes = input.Notes?.Trim(),
            EffectiveFrom = input.EffectiveFrom == default ? today : input.EffectiveFrom,
            Status = EntityStatus.Active,
            CreatedAt = now
        });
    }

    // ================= CUSTOMERS =================
    public async Task<object> ListCustomersAsync(CurrentUser user, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.OrganizationId == user.OrganizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c => c.FullName.Contains(s) || (c.Phone != null && c.Phone.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.FullName,
                c.Phone,
                c.Notes,
                c.LoyaltyPoints,
                VisitCount = db.Sales.Count(s => s.CustomerId == c.Id && s.Status == SaleStatus.Completed),
                TotalSpend = db.Sales.Where(s => s.CustomerId == c.Id && s.Status == SaleStatus.Completed).Sum(s => (decimal?)s.TotalAmount) ?? 0m,
                LastVisit = db.Sales.Where(s => s.CustomerId == c.Id && s.Status == SaleStatus.Completed).Max(s => (DateTime?)s.CreatedAt)
            }).ToListAsync(ct);

        return new { totalCount = total, page, pageSize, items };
    }

    public async Task<Guid> CreateCustomerAsync(CurrentUser user, CustomerInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.CustomerManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة العملاء");
        if (string.IsNullOrWhiteSpace(input.FullName)) throw AppException.Bad("name_required", "اسم العميل مطلوب");

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            FullName = input.FullName.Trim(),
            Phone = input.Phone?.Trim(),
            Notes = input.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);
        db.AuditLogs.Add(Audit("customer.created", nameof(Customer), customer.Id.ToString(), $"name={customer.FullName}", user, now));
        await db.SaveChangesAsync(ct);
        return customer.Id;
    }

    public async Task UpdateCustomerAsync(CurrentUser user, Guid id, CustomerInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.CustomerManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة العملاء");
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("العميل غير موجود");
        if (string.IsNullOrWhiteSpace(input.FullName)) throw AppException.Bad("name_required", "اسم العميل مطلوب");

        customer.FullName = input.FullName.Trim();
        customer.Phone = input.Phone?.Trim();
        customer.Notes = input.Notes?.Trim();
        customer.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(Audit("customer.updated", nameof(Customer), customer.Id.ToString(), $"name={customer.FullName}", user, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task<object> GetCustomerAsync(CurrentUser user, Guid id, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("العميل غير موجود");

        var visits = await db.Sales.AsNoTracking()
            .Where(s => s.CustomerId == id && s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt).Take(200)
            .Select(s => new
            {
                s.Id,
                s.ReceiptNo,
                s.CreatedAt,
                s.TotalAmount,
                StatusText = s.Status == SaleStatus.Completed ? "مكتملة" : "ملغاة",
                PaymentText = s.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                ServicesText = string.Join(" + ", s.Items.Select(i => i.ServiceName + (i.Quantity > 1 ? $" ×{i.Quantity}" : ""))),
                BarberName = db.Barbers.Where(b => b.Id == s.BarberId).Select(b => b.FullName).First(),
                BranchName = db.Branches.Where(b => b.Id == s.BranchId).Select(b => b.Name).First()
            }).ToListAsync(ct);

        var summary = await db.Sales.AsNoTracking().Where(s => s.CustomerId == id && s.Status == SaleStatus.Completed)
            .GroupBy(s => 1).Select(g => new
            {
                Total = g.Sum(x => x.TotalAmount),
                Count = g.Count(),
                LastVisit = g.Max(x => x.CreatedAt)
            }).FirstOrDefaultAsync(ct);

        return new
        {
            customer = new { customer.Id, customer.FullName, customer.Phone, customer.Notes, customer.CreatedAt, customer.LoyaltyPoints },
            appointments = await db.Appointments.AsNoTracking()
                .Where(a => a.CustomerId == id && a.StartAt >= DateTime.UtcNow.AddDays(-1)
                            && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Completed)
                .OrderBy(a => a.StartAt).Take(10)
                .Select(a => new { a.Id, a.StartAt, a.Status, a.Reference, BranchName = db.Branches.Where(b => b.Id == a.BranchId).Select(b => b.Name).First() })
                .ToListAsync(ct),
            summary = new
            {
                Total = summary?.Total ?? 0m,
                Count = summary?.Count ?? 0,
                LastVisit = summary?.LastVisit
            },
            visits
        };
    }

    private static AuditLog Audit(string action, string entityType, string entityId, string details, CurrentUser user, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = now
        };
}
