using Hallagi.Application.Tax;

namespace Hallagi.Application.Commerce;

public class CouponService(IAppDbContext db)
{
    /// <summary>Returns the discount a coupon grants on the given gross, or throws an Arabic error.</summary>
    public static decimal Evaluate(Coupon coupon, decimal gross, DateOnly today)
    {
        if (coupon.Status != EntityStatus.Active)
            throw AppException.Bad("coupon_inactive", "كود الخصم غير مفعّل");
        if (coupon.ValidFrom is not null && today < coupon.ValidFrom)
            throw AppException.Bad("coupon_not_started", "كود الخصم لم يبدأ بعد");
        if (coupon.ValidTo is not null && today > coupon.ValidTo)
            throw AppException.Bad("coupon_expired", "انتهت صلاحية كود الخصم");
        if (coupon.MaxUses is not null && coupon.UsedCount >= coupon.MaxUses)
            throw AppException.Bad("coupon_exhausted", "تم استخدام كود الخصم بالحد الأقصى");
        if (coupon.MinSubtotal is not null && gross < coupon.MinSubtotal)
            throw AppException.Bad("coupon_min_subtotal", $"كود الخصم يتطلب فاتورة بقيمة {coupon.MinSubtotal:0.00} ر.س على الأقل");

        var discount = coupon.Type == DiscountType.Percent
            ? gross * coupon.Value / 100m
            : coupon.Value;
        if (coupon.MaxDiscount is not null) discount = Math.Min(discount, coupon.MaxDiscount.Value);
        return Pricing.Round(Math.Min(discount, gross));
    }

    public async Task<object> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.CouponManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة أكواد الخصم");
        var items = await db.Coupons.AsNoTracking()
            .Where(c => c.OrganizationId == user.OrganizationId)
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return new
        {
            items = items.Select(c => new
            {
                c.Id, c.Code, c.Description, c.Type, c.Value, c.MinSubtotal, c.MaxDiscount,
                c.MaxUses, c.UsedCount, c.ValidFrom, c.ValidTo, c.Status,
                TypeText = c.Type == DiscountType.Percent ? $"{c.Value:0.##}%" : $"{c.Value:0.00} ر.س",
                StatusText = c.Status == EntityStatus.Active ? "مفعّل" : "موقوف"
            })
        };
    }

    public async Task<Guid> CreateAsync(CurrentUser user, CouponInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.CouponManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة أكواد الخصم");
        var code = Validate(input);
        if (await db.Coupons.AnyAsync(c => c.OrganizationId == user.OrganizationId && c.Code == code, ct))
            throw AppException.Conflict("duplicate_code", "هذا الكود مستخدم مسبقًا");

        var now = DateTime.UtcNow;
        var coupon = new Coupon { Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, CreatedAt = now };
        Apply(coupon, input, code, now);
        db.Coupons.Add(coupon);
        db.AuditLogs.Add(Audit.Entry(user, "coupon.created", nameof(Coupon), coupon.Id, $"code={code}", now));
        await db.SaveChangesAsync(ct);
        return coupon.Id;
    }

    public async Task UpdateAsync(CurrentUser user, Guid id, CouponInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.CouponManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة أكواد الخصم");
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("كود الخصم غير موجود");
        var code = Validate(input);
        if (await db.Coupons.AnyAsync(c => c.OrganizationId == user.OrganizationId && c.Code == code && c.Id != id, ct))
            throw AppException.Conflict("duplicate_code", "هذا الكود مستخدم مسبقًا");
        var now = DateTime.UtcNow;
        Apply(coupon, input, code, now);
        db.AuditLogs.Add(Audit.Entry(user, "coupon.updated", nameof(Coupon), coupon.Id, $"code={code}", now));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>POS helper: checks a code against the current cart without consuming it.</summary>
    public async Task<object> PreviewAsync(CurrentUser user, string code, decimal gross, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleCreate))
            throw AppException.Forbidden("ليس لديك صلاحية");
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.OrganizationId == user.OrganizationId && c.Code == normalized, ct)
            ?? throw AppException.Bad("invalid_coupon", "كود الخصم غير صحيح");
        var tz = OrgTime.Resolve(await db.Organizations.Where(o => o.Id == user.OrganizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct));
        var discount = Evaluate(coupon, gross, OrgTime.Today(tz));
        return new { coupon.Code, coupon.Description, discount };
    }

    private static string Validate(CouponInput input)
    {
        var code = (input.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length is < 3 or > 40 || !code.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'))
            throw AppException.Bad("invalid_code", "الكود من 3 إلى 40 حرفًا إنجليزيًا أو رقمًا");
        if (input.Value <= 0)
            throw AppException.Bad("invalid_value", "قيمة الخصم يجب أن تكون أكبر من صفر");
        if (input.Type == DiscountType.Percent && input.Value > 100)
            throw AppException.Bad("invalid_value", "نسبة الخصم لا تتجاوز 100%");
        if (input.ValidFrom is not null && input.ValidTo is not null && input.ValidTo < input.ValidFrom)
            throw AppException.Bad("invalid_dates", "تاريخ الانتهاء قبل تاريخ البداية");
        if (input.MaxUses is <= 0)
            throw AppException.Bad("invalid_max_uses", "عدد مرات الاستخدام غير صحيح");
        return code;
    }

    private static void Apply(Coupon c, CouponInput input, string code, DateTime now)
    {
        c.Code = code;
        c.Description = input.Description?.Trim();
        c.Type = input.Type;
        c.Value = input.Value;
        c.MinSubtotal = input.MinSubtotal is > 0 ? input.MinSubtotal : null;
        c.MaxDiscount = input.MaxDiscount is > 0 ? input.MaxDiscount : null;
        c.MaxUses = input.MaxUses;
        c.ValidFrom = input.ValidFrom;
        c.ValidTo = input.ValidTo;
        c.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        c.UpdatedAt = now;
    }
}
