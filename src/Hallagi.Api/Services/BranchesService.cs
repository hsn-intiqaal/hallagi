using Hallagi.Api.Data;
using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hallagi.Api.Services;

public class BranchesService(AppDbContext db, SubscriptionService subscriptions)
{
    public async Task<object> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId)
            .ToListAsync(ct);

        var items = new List<object>();
        foreach (var b in branches)
        {
            var barberCount = await db.BarberBranchAssignments.CountAsync(a => a.BranchId == b.Id, ct);
            var userCount = await db.MemberBranchAccess.CountAsync(a => a.BranchId == b.Id, ct);
            var openShift = await db.Shifts.AsNoTracking()
                .AnyAsync(s => s.BranchId == b.Id && s.Status == ShiftStatus.Open, ct);
            items.Add(new
            {
                b.Id,
                b.Name,
                b.City,
                b.Address,
                b.Phone,
                OpensAt = b.OpensAt.ToString("HH\\:mm"),
                ClosesAt = b.ClosesAt.ToString("HH\\:mm"),
                b.Status,
                StatusText = b.Status == EntityStatus.Active ? "نشط" : "غير نشط",
                barberCount,
                userCount,
                hasOpenShift = openShift
            });
        }
        return new { items };
    }

    public async Task<Guid> CreateAsync(CurrentUser user, BranchInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BranchManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة الفروع");
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City))
            throw ApiException.Bad("missing_fields", "اسم الفرع والمدينة مطلوبان");

        await subscriptions.EnsureBranchLimitAsync(user.OrganizationId, ct);

        var now = DateTime.UtcNow;
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            Name = input.Name.Trim(),
            City = input.City.Trim(),
            Address = input.Address?.Trim(),
            Phone = input.Phone?.Trim(),
            OpensAt = ParseHour(input.OpensAt, new TimeOnly(9, 0)),
            ClosesAt = ParseHour(input.ClosesAt, new TimeOnly(23, 0)),
            Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive,
            CreatedAt = now,
            UpdatedAt = now,
            Drawer = new CashDrawer { OrganizationId = user.OrganizationId, CreatedAt = now }
        };
        db.Branches.Add(branch);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "branch.created",
            EntityType = nameof(Branch),
            EntityId = branch.Id.ToString(),
            Details = $"name={branch.Name}; city={branch.City}",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        return branch.Id;
    }

    public async Task UpdateAsync(CurrentUser user, Guid id, BranchInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.BranchManage))
            throw ApiException.Forbidden("ليس لديك صلاحية لإدارة الفروع");
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("الفرع غير موجود");
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.City))
            throw ApiException.Bad("missing_fields", "اسم الفرع والمدينة مطلوبان");

        branch.Name = input.Name.Trim();
        branch.City = input.City.Trim();
        branch.Address = input.Address?.Trim();
        branch.Phone = input.Phone?.Trim();
        branch.OpensAt = ParseHour(input.OpensAt, branch.OpensAt);
        branch.ClosesAt = ParseHour(input.ClosesAt, branch.ClosesAt);
        branch.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        branch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static TimeOnly ParseHour(string? value, TimeOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return TimeOnly.TryParseExact(value.Trim(), "HH:mm", out var t)
            ? t
            : throw ApiException.Bad("invalid_hours", "صيغة ساعات العمل غير صحيحة (HH:mm)");
    }
}

public class SettingsService(AppDbContext db)
{
    public async Task<object> GetAsync(CurrentUser user, CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("المنشأة غير موجودة");
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
            throw ApiException.Forbidden("ليس لديك صلاحية لتعديل الإعدادات");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw ApiException.NotFound("المنشأة غير موجودة");
        if (string.IsNullOrWhiteSpace(req.OrganizationName))
            throw ApiException.Bad("name_required", "اسم المنشأة مطلوب");

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
                throw ApiException.Bad("invalid_discount_cap", "حد الخصم اليدوي بين 0 و100%");
            settings.MaxManualDiscountPercent = req.MaxManualDiscountPercent.Value;
        }

        if (req.Tax is { } tax)
        {
            var vatNumber = string.IsNullOrWhiteSpace(tax.VatNumber) ? null : tax.VatNumber.Trim();
            if (tax.VatEnabled && !Pricing.IsValidVatNumber(vatNumber))
                throw ApiException.Bad("invalid_vat_number", "الرقم الضريبي يجب أن يكون 15 رقمًا يبدأ وينتهي بالرقم 3");
            if (tax.VatRate is < 0 or > 1)
                throw ApiException.Bad("invalid_vat_rate", "نسبة الضريبة غير صحيحة");
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
                throw ApiException.Bad("invalid_loyalty", "إعدادات الولاء غير صحيحة");
            settings.LoyaltyEnabled = loyalty.Enabled;
            settings.LoyaltyPointsPerSar = loyalty.PointsPerSar;
            settings.LoyaltyPointValue = loyalty.PointValue;
            settings.LoyaltyMinRedeem = loyalty.MinRedeem;
        }

        if (req.Booking is { } booking)
        {
            if (booking.SlotMinutes is < 5 or > 120 || booking.MaxDaysAhead is < 1 or > 90)
                throw ApiException.Bad("invalid_booking", "إعدادات الحجز غير صحيحة");
            if (!string.IsNullOrWhiteSpace(booking.Slug))
            {
                var slug = Slugs.Normalize(booking.Slug);
                if (await db.Organizations.AnyAsync(o => o.Slug == slug && o.Id != org.Id, ct))
                    throw ApiException.Conflict("slug_taken", "رابط الحجز مستخدم لمنشأة أخرى");
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
            throw ApiException.Forbidden("ليس لديك صلاحية لإعداد الخدمات");

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

public record BranchInput(string Name, string City, string? Address, bool Active, string? Phone = null, string? OpensAt = null, string? ClosesAt = null);
public record UpdateSettingsRequest(
    string OrganizationName,
    string? Currency,
    string? TimeZone,
    string? LogoUrl,
    bool EnableCash,
    bool EnableCard,
    string? ReceiptFooter,
    string? ReceiptPhone,
    string? CoverImageUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    TaxSettingsInput? Tax = null,
    LoyaltySettingsInput? Loyalty = null,
    BookingSettingsInput? Booking = null,
    decimal? MaxManualDiscountPercent = null);

public record TaxSettingsInput(bool VatEnabled, decimal VatRate, bool PricesIncludeVat, string? VatNumber, string? CommercialRegistration, string? LegalName, string? TaxAddress);
public record LoyaltySettingsInput(bool Enabled, decimal PointsPerSar, decimal PointValue, int MinRedeem);
public record BookingSettingsInput(bool Enabled, string? Slug, int SlotMinutes, int MaxDaysAhead);

public static class Slugs
{
    private static readonly HashSet<string> Reserved = new() { "admin", "api", "app", "book", "login", "platform", "hallagi", "www" };

    /// <summary>Lower-case latin letters, digits and dashes (3–40 chars).</summary>
    public static string Normalize(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        if (s.Length is < 3 or > 40 || !s.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
            || s.StartsWith('-') || s.EndsWith('-') || Reserved.Contains(s))
            throw ApiException.Bad("invalid_slug", "رابط الحجز: من 3 إلى 40 حرفًا إنجليزيًا صغيرًا أو رقمًا أو شرطة (-)");
        return s;
    }

    public static async Task<string> RandomUniqueAsync(AppDbContext db, CancellationToken ct)
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
        while (true)
        {
            var slug = "salon-" + new string(Enumerable.Range(0, 6)
                .Select(_ => alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
            if (!await db.Organizations.AnyAsync(o => o.Slug == slug, ct)) return slug;
        }
    }
}
