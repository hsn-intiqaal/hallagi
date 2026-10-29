namespace Hallagi.Application.Sales;

/// <summary>Everything the POS screen needs in one round-trip, scoped to a single branch.</summary>
public class PosService(IAppDbContext db)
{
    public async Task<object> GetDataAsync(CurrentUser user, Guid branchId, CancellationToken ct = default)
    {
        if (user.Role == UserRole.Barber)
        {
            // a barber operates at his assigned branches only
            var barberBranches = await db.BarberBranchAssignments.AsNoTracking()
                .Where(a => db.Barbers.Any(b => b.Id == a.BarberId && b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId))
                .Select(a => a.BranchId).ToListAsync(ct);
            if (!barberBranches.Contains(branchId))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }
        else if (!Permissions.CanAccessBranch(user, branchId))
        {
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }

        var settings = await db.OrganizationSettings.AsNoTracking()
                           .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct)
                       ?? new OrganizationSettings { OrganizationId = user.OrganizationId };

        var services = await db.Services.AsNoTracking()
            .Where(s => s.OrganizationId == user.OrganizationId && s.Status == EntityStatus.Active)
            .OrderBy(s => s.Name).ToListAsync(ct);
        var serviceIds = services.Select(s => s.Id).ToList();

        var branchPrices = await db.BranchServicePrices.AsNoTracking()
            .Where(p => p.BranchId == branchId && serviceIds.Contains(p.ServiceId))
            .ToDictionaryAsync(p => p.ServiceId, p => p.Price, ct);

        var barbers = await db.Barbers.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active
                        && b.BranchAssignments.Any(a => a.BranchId == branchId))
            .OrderBy(b => b.FullName)
            .Select(b => new
            {
                b.Id,
                b.FullName,
                Initials = b.FullName.Substring(0, 1),
                BranchNames = b.BranchAssignments.Where(a => a.BranchId == branchId)
                    .Select(a => db.Branches.Where(br => br.Id == a.BranchId).Select(br => br.Name).First()).ToList()
            }).ToListAsync(ct);

        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.OrganizationId == user.OrganizationId)
            .OrderByDescending(c => c.UpdatedAt).Take(200)
            .Select(c => new { c.Id, c.FullName, c.Phone, c.LoyaltyPoints }).ToListAsync(ct);

        var products = await db.Products.AsNoTracking()
            .Where(p => p.OrganizationId == user.OrganizationId && p.Status == EntityStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Price, p.Barcode,
                Stock = p.Stock.Where(s => s.BranchId == branchId).Select(s => (int?)s.Quantity).FirstOrDefault() ?? 0
            }).ToListAsync(ct);

        return new
        {
            branch = await db.Branches.Where(b => b.Id == branchId).Select(b => new { b.Id, b.Name, b.City }).FirstAsync(ct),
            services = services.Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                Price = branchPrices.TryGetValue(s.Id, out var p) ? p : s.BasePrice,
                s.DurationMinutes
            }),
            products,
            barbers,
            customers,
            pricing = new
            {
                vatEnabled = settings.VatEnabled,
                vatRate = settings.VatRate,
                pricesIncludeVat = settings.PricesIncludeVat,
                canDiscount = Permissions.Can(user, Permissions.SaleDiscount),
                maxManualDiscountPercent = user.Role == UserRole.Owner ? 100m : settings.MaxManualDiscountPercent,
                loyaltyEnabled = settings.LoyaltyEnabled,
                loyaltyPointValue = settings.LoyaltyPointValue,
                loyaltyPointsPerSar = settings.LoyaltyPointsPerSar,
                loyaltyMinRedeem = settings.LoyaltyMinRedeem
            },
            payment = new
            {
                cashEnabled = settings.EnableCash,
                cardEnabled = settings.EnableCard,
                methods = new[]
                {
                    new { value = PaymentMethod.Cash.ToString(), label = "نقدي", enabled = settings.EnableCash },
                    new { value = PaymentMethod.Card.ToString(), label = "بطاقة", enabled = settings.EnableCard }
                }
            }
        };
    }
}
