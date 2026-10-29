using Hallagi.Application.Tax;
using Hallagi.Application.Catalog;

namespace Hallagi.Application.Organizations;

public class SettingsService(IAppDbContext db)
{
    public async Task<object> GetAsync(CurrentUser user, CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw AppException.NotFound("المنشأة غير موجودة");
        var s = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == org.Id, ct)
                ?? new OrganizationSettings { OrganizationId = org.Id };
        return new
        {
            organization = new { org.Id, org.Name, org.Currency, org.TimeZone, org.LogoUrl, org.PrimaryColor, org.SecondaryColor, org.CoverImageUrl, org.Slug },
            settings = new
            {
                s.EnableCash, s.EnableCard, s.ReceiptFooter, s.ReceiptPhone, s.MaxManualDiscountPercent
            },
            tax = new { s.VatEnabled, s.VatRate, s.PricesIncludeVat, s.VatNumber, s.CommercialRegistration, s.LegalName, s.TaxAddress },
            loyalty = new { Enabled = s.LoyaltyEnabled, PointsPerSar = s.LoyaltyPointsPerSar, PointValue = s.LoyaltyPointValue, MinRedeem = s.LoyaltyMinRedeem },
            booking = new { Enabled = s.OnlineBookingEnabled, org.Slug, SlotMinutes = s.BookingSlotMinutes, MaxDaysAhead = s.BookingMaxDaysAhead }
        };
    }

    public async Task UpdateAsync(CurrentUser user, UpdateSettingsRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettingsManage))
            throw AppException.Forbidden("ليس لديك صلاحية لتعديل الإعدادات");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw AppException.NotFound("المنشأة غير موجودة");
        if (string.IsNullOrWhiteSpace(req.OrganizationName))
            throw AppException.Bad("name_required", "اسم المنشأة مطلوب");

        org.Name = req.OrganizationName.Trim();
        org.Currency = string.IsNullOrWhiteSpace(req.Currency) ? org.Currency : req.Currency.Trim();
        org.TimeZone = string.IsNullOrWhiteSpace(req.TimeZone) ? org.TimeZone : req.TimeZone.Trim();
        // null = keep current image, "" = remove, data URL = replace
        if (req.LogoUrl is not null) org.LogoUrl = Branding.NormalizeImage(req.LogoUrl);
        if (req.CoverImageUrl is not null) org.CoverImageUrl = Branding.NormalizeImage(req.CoverImageUrl);
        org.PrimaryColor = Branding.NormalizeColor(req.PrimaryColor, org.PrimaryColor);
        org.SecondaryColor = Branding.NormalizeColor(req.SecondaryColor, org.SecondaryColor);
        org.UpdatedAt = DateTime.UtcNow;

        var settings = await db.OrganizationSettings.FirstOrDefaultAsync(s => s.OrganizationId == org.Id, ct);
        if (settings is null)
        {
            settings = new OrganizationSettings { OrganizationId = org.Id };
            db.OrganizationSettings.Add(settings);
        }
        settings.EnableCash = req.EnableCash;
        settings.EnableCard = req.EnableCard;
        settings.ReceiptFooter = req.ReceiptFooter?.Trim();
        settings.ReceiptPhone = req.ReceiptPhone?.Trim();
        if (req.MaxManualDiscountPercent is not null)
        {
            if (req.MaxManualDiscountPercent is < 0 or > 100)
                throw AppException.Bad("invalid_discount_cap", "حد الخصم اليدوي بين 0 و100%");
            settings.MaxManualDiscountPercent = req.MaxManualDiscountPercent.Value;
        }

        if (req.Tax is { } tax)
        {
            var vatNumber = string.IsNullOrWhiteSpace(tax.VatNumber) ? null : tax.VatNumber.Trim();
            if (tax.VatEnabled && !Pricing.IsValidVatNumber(vatNumber))
                throw AppException.Bad("invalid_vat_number", "الرقم الضريبي يجب أن يكون 15 رقمًا يبدأ وينتهي بالرقم 3");
            if (tax.VatRate is < 0 or > 1)
                throw AppException.Bad("invalid_vat_rate", "نسبة الضريبة غير صحيحة");
            settings.VatEnabled = tax.VatEnabled;
            settings.VatRate = tax.VatRate;
            settings.PricesIncludeVat = tax.PricesIncludeVat;
            settings.VatNumber = vatNumber;
            settings.CommercialRegistration = tax.CommercialRegistration?.Trim();
            settings.LegalName = tax.LegalName?.Trim();
            settings.TaxAddress = tax.TaxAddress?.Trim();
        }

        if (req.Loyalty is { } loyalty)
        {
            if (loyalty.PointsPerSar < 0 || loyalty.PointValue < 0 || loyalty.MinRedeem < 0)
                throw AppException.Bad("invalid_loyalty", "إعدادات الولاء غير صحيحة");
            settings.LoyaltyEnabled = loyalty.Enabled;
            settings.LoyaltyPointsPerSar = loyalty.PointsPerSar;
            settings.LoyaltyPointValue = loyalty.PointValue;
            settings.LoyaltyMinRedeem = loyalty.MinRedeem;
        }

        if (req.Booking is { } booking)
        {
            if (booking.SlotMinutes is < 5 or > 120 || booking.MaxDaysAhead is < 1 or > 90)
                throw AppException.Bad("invalid_booking", "إعدادات الحجز غير صحيحة");
            if (!string.IsNullOrWhiteSpace(booking.Slug))
            {
                var slug = Slugs.Normalize(booking.Slug);
                if (await db.Organizations.AnyAsync(o => o.Slug == slug && o.Id != org.Id, ct))
                    throw AppException.Conflict("slug_taken", "رابط الحجز مستخدم لمنشأة أخرى");
                org.Slug = slug;
            }
            else if (booking.Enabled && org.Slug is null)
            {
                org.Slug = await Slugs.RandomUniqueAsync(db, ct);
            }
            settings.OnlineBookingEnabled = booking.Enabled;
            settings.BookingSlotMinutes = booking.SlotMinutes;
            settings.BookingMaxDaysAhead = booking.MaxDaysAhead;
        }

        db.AuditLogs.Add(Audit.Entry(user, "settings.updated", nameof(OrganizationSettings), org.Id,
            $"vat={settings.VatEnabled}; loyalty={settings.LoyaltyEnabled}; booking={settings.OnlineBookingEnabled}", DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Onboarding step 1/2/3/4 support: bulk add services and barbers for a new organization.</summary>
    public async Task BulkSeedAsync(CurrentUser user, List<string> serviceNames, List<BarberInput> barbers, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ServiceManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإعداد الخدمات");

        var now = DateTime.UtcNow;
        foreach (var name in serviceNames.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (await db.Services.AnyAsync(s => s.OrganizationId == user.OrganizationId && s.Name == name.Trim(), ct)) continue;
            db.Services.Add(new Service
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                Name = name.Trim(),
                BasePrice = 25m,
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var b in barbers)
        {
            var barber = new Barber
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                FullName = b.FullName.Trim(),
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Barbers.Add(barber);
            foreach (var br in b.BranchIds.Distinct())
                db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barber.Id, BranchId = br });
        }

        await db.SaveChangesAsync(ct);
    }
}
