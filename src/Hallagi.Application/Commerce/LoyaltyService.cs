using Hallagi.Application.Tax;

namespace Hallagi.Application.Commerce;

public class LoyaltyService(IAppDbContext db)
{
    public async Task<object> HistoryAsync(CurrentUser user, Guid customerId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.CustomerManage))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض العملاء");
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("العميل غير موجود");
        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);
        var items = await db.LoyaltyTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId).OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct);
        return new
        {
            balance = customer.LoyaltyPoints,
            balanceValue = Pricing.Round(customer.LoyaltyPoints * (settings?.LoyaltyPointValue ?? 0m)),
            enabled = settings?.LoyaltyEnabled ?? false,
            items = items.Select(t => new
            {
                t.Id, t.CreatedAt, t.Type, t.Points, t.BalanceAfter, t.Note, t.SaleId,
                TypeText = t.Type switch
                {
                    LoyaltyTxType.Earn => "اكتساب",
                    LoyaltyTxType.Redeem => "استبدال",
                    LoyaltyTxType.Reverse => "عكس",
                    _ => "تعديل يدوي"
                }
            })
        };
    }

    public async Task AdjustAsync(CurrentUser user, Guid customerId, int points, string? note, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SettingsManage))
            throw AppException.Forbidden("تعديل النقاط يدويًا يتطلب صلاحية إدارة الإعدادات");
        if (points == 0) throw AppException.Bad("invalid_points", "أدخل عدد النقاط");
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("العميل غير موجود");
        if (customer.LoyaltyPoints + points < 0)
            throw AppException.Bad("negative_points", "لا يمكن أن يصبح رصيد النقاط سالبًا");
        var now = DateTime.UtcNow;
        customer.LoyaltyPoints += points;
        customer.UpdatedAt = now;
        db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, CustomerId = customerId,
            Type = LoyaltyTxType.Adjust, Points = points, BalanceAfter = customer.LoyaltyPoints,
            Note = string.IsNullOrWhiteSpace(note) ? "تعديل يدوي" : note.Trim(), CreatedBy = user.ProfileId, CreatedAt = now
        });
        db.AuditLogs.Add(Audit.Entry(user, "loyalty.adjusted", nameof(Customer), customerId, $"points={points}", now));
        await db.SaveChangesAsync(ct);
    }
}
