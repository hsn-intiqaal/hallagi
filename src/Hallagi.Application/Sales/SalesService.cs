using Hallagi.Application.Tax;
using Hallagi.Application.Commerce;

namespace Hallagi.Application.Sales;

public class SalesService(IAppDbContext db, OrgClock clock)
{
    public async Task<object> CreateAsync(CurrentUser user, CreateSaleRequest req, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleCreate))
            throw AppException.Forbidden("ليس لديك صلاحية لتسجيل المبيعات");

        // Barber-operated POS: a barber may only record sales for himself
        if (user.Role == UserRole.Barber)
        {
            var linkedBarberId = await db.Barbers.AsNoTracking()
                .Where(b => b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
            if (linkedBarberId is null || req.BarberId != linkedBarberId.Value)
                throw AppException.Forbidden("يمكن للحلاق تسجيل المبيعات لنفسه فقط");

            // A barber may sell at any branch he is assigned to
            var allowedBarberBranches = await db.BarberBranchAssignments.AsNoTracking()
                .Where(a => a.BarberId == linkedBarberId.Value).Select(a => a.BranchId).ToListAsync(ct);
            if (!allowedBarberBranches.Contains(req.BranchId))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }
        else if (!Permissions.CanAccessBranch(user, req.BranchId))
        {
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }

        if (req.Items is null || req.Items.Count == 0)
            throw AppException.Bad("empty_cart", "أضف خدمة واحدة على الأقل لإتمام البيع");
        if (req.Items.Any(i => i.Quantity <= 0 || i.Quantity > 50))
            throw AppException.Bad("invalid_quantity", "الكمية غير صحيحة");
        if (req.Items.Any(i => (i.ServiceId is null) == (i.ProductId is null)))
            throw AppException.Bad("invalid_item", "كل بند يجب أن يكون خدمة أو منتجًا");
        if (req.PaymentMethod != PaymentMethod.Cash && req.PaymentMethod != PaymentMethod.Card)
            throw AppException.Bad("invalid_payment", "طريقة الدفع غير مدعومة");
        if (req.RedeemPoints < 0)
            throw AppException.Bad("invalid_points", "عدد النقاط غير صحيح");
        if (req.ManualDiscount is < 0)
            throw AppException.Bad("invalid_discount", "قيمة الخصم غير صحيحة");

        var settings = await db.OrganizationSettings.FindAsync(new object[] { user.OrganizationId }, ct)
                       ?? new OrganizationSettings { OrganizationId = user.OrganizationId };
        if (req.PaymentMethod == PaymentMethod.Cash && !settings.EnableCash)
            throw AppException.Bad("payment_disabled", "طريقة الدفع النقدي معطلة في إعدادات المنشأة");
        if (req.PaymentMethod == PaymentMethod.Card && !settings.EnableCard)
            throw AppException.Bad("payment_disabled", "طريقة الدفع بالبطاقة معطلة في إعدادات المنشأة");

        // Idempotent replay: same key returns the same stored sale
        if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
        {
            var existing = await db.Sales
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.OrganizationId == user.OrganizationId && x.IdempotencyKey == req.IdempotencyKey, ct);
            if (existing is not null)
                return new { replayed = true, sale = await MapSaleAsync(existing, ct) };
        }

        var branch = await db.Branches.FirstOrDefaultAsync(x => x.Id == req.BranchId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.Bad("invalid_branch", "الفرع غير موجود");

        var barber = await db.Barbers.FirstOrDefaultAsync(x => x.Id == req.BarberId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.Bad("invalid_barber", "الحلاق غير موجود");
        if (barber.Status != EntityStatus.Active)
            throw AppException.Bad("barber_inactive", "حساب الحلاق غير نشط");
        var barberWorksHere = await db.BarberBranchAssignments.AnyAsync(a => a.BarberId == barber.Id && a.BranchId == branch.Id, ct);
        if (!barberWorksHere)
            throw AppException.Bad("barber_not_in_branch", "الحلاق غير مسند لهذا الفرع");

        Customer? customer = null;
        if (req.CustomerId is not null)
        {
            customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == req.CustomerId && x.OrganizationId == user.OrganizationId, ct)
                ?? throw AppException.Bad("invalid_customer", "العميل غير موجود");
        }

        Appointment? appointment = null;
        if (req.AppointmentId is not null)
        {
            appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == req.AppointmentId && a.OrganizationId == user.OrganizationId, ct)
                ?? throw AppException.Bad("invalid_appointment", "الموعد غير موجود");
            if (appointment.BranchId != branch.Id)
                throw AppException.Bad("appointment_branch", "الموعد مسجل في فرع آخر");
            if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
                throw AppException.Conflict("appointment_closed", "هذا الموعد مغلق ولا يمكن تحصيله");
        }

        // ===== Server-side pricing from the trusted source =====
        var items = new List<SaleItem>();

        var serviceIds = req.Items.Where(i => i.ServiceId is not null).Select(i => i.ServiceId!.Value).Distinct().ToList();
        if (serviceIds.Count > 0)
        {
            var services = await db.Services.Where(s => s.OrganizationId == user.OrganizationId && serviceIds.Contains(s.Id)).ToListAsync(ct);
            if (services.Count != serviceIds.Count)
                throw AppException.Bad("invalid_service", "إحدى الخدمات غير موجودة");
            if (services.Any(s => s.Status != EntityStatus.Active))
                throw AppException.Bad("service_inactive", "إحدى الخدمات غير نشطة");

            var branchPrices = await db.BranchServicePrices
                .Where(p => p.BranchId == branch.Id && serviceIds.Contains(p.ServiceId))
                .ToDictionaryAsync(p => p.ServiceId, p => p.Price, ct);

            foreach (var reqItem in req.Items.Where(i => i.ServiceId is not null))
            {
                var svc = services.First(s => s.Id == reqItem.ServiceId);
                var unitPrice = branchPrices.TryGetValue(svc.Id, out var bp) ? bp : svc.BasePrice;
                items.Add(new SaleItem
                {
                    Id = Guid.NewGuid(),
                    ItemType = SaleItemType.Service,
                    ServiceId = svc.Id,
                    ServiceName = svc.Name,
                    UnitPrice = unitPrice,
                    Quantity = reqItem.Quantity,
                    LineTotal = unitPrice * reqItem.Quantity
                });
            }
        }

        var productQty = req.Items.Where(i => i.ProductId is not null)
            .GroupBy(i => i.ProductId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var stockRows = new Dictionary<Guid, ProductStock>();
        if (productQty.Count > 0)
        {
            var productIds = productQty.Keys.ToList();
            var products = await db.Products.Where(p => p.OrganizationId == user.OrganizationId && productIds.Contains(p.Id)).ToListAsync(ct);
            if (products.Count != productIds.Count)
                throw AppException.Bad("invalid_product", "أحد المنتجات غير موجود");
            if (products.Any(p => p.Status != EntityStatus.Active))
                throw AppException.Bad("product_inactive", "أحد المنتجات غير نشط");

            stockRows = await db.ProductStocks.Where(s => s.BranchId == branch.Id && productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId, ct);
            foreach (var p in products)
            {
                var onHand = stockRows.TryGetValue(p.Id, out var st) ? st.Quantity : 0;
                if (onHand < productQty[p.Id])
                    throw AppException.Bad("insufficient_stock", $"الكمية المتوفرة من «{p.Name}» في هذا الفرع {onHand} فقط");
                items.Add(new SaleItem
                {
                    Id = Guid.NewGuid(),
                    ItemType = SaleItemType.Product,
                    ProductId = p.Id,
                    ServiceName = p.Name,
                    UnitPrice = p.Price,
                    UnitCost = p.Cost,
                    Quantity = productQty[p.Id],
                    LineTotal = p.Price * productQty[p.Id]
                });
            }
        }

        var gross = items.Sum(i => i.LineTotal);

        // ===== Discounts: coupon + manual + loyalty points (all before VAT) =====
        Coupon? coupon = null;
        decimal couponDiscount = 0m;
        var tz = OrgTime.Resolve((await db.Organizations.AsNoTracking().Where(o => o.Id == user.OrganizationId)
            .Select(o => o.TimeZone).FirstOrDefaultAsync(ct)));
        if (!string.IsNullOrWhiteSpace(req.CouponCode))
        {
            var code = req.CouponCode.Trim().ToUpperInvariant();
            coupon = await db.Coupons.FirstOrDefaultAsync(c => c.OrganizationId == user.OrganizationId && c.Code == code, ct)
                ?? throw AppException.Bad("invalid_coupon", "كود الخصم غير صحيح");
            couponDiscount = CouponService.Evaluate(coupon, gross, OrgTime.Today(tz));
        }

        decimal manualDiscount = Pricing.Round(req.ManualDiscount ?? 0m);
        if (manualDiscount > 0)
        {
            if (!Permissions.Can(user, Permissions.SaleDiscount))
                throw AppException.Forbidden("ليس لديك صلاحية لمنح خصم يدوي");
            var cap = Pricing.Round(gross * settings.MaxManualDiscountPercent / 100m);
            if (user.Role != UserRole.Owner && manualDiscount > cap)
                throw AppException.Bad("discount_too_high", $"الحد الأعلى للخصم اليدوي {settings.MaxManualDiscountPercent:0.##}% ({cap:0.00} ر.س)");
        }

        decimal pointsDiscount = 0m;
        if (req.RedeemPoints > 0)
        {
            if (!settings.LoyaltyEnabled)
                throw AppException.Bad("loyalty_disabled", "برنامج الولاء غير مفعّل");
            if (customer is null)
                throw AppException.Bad("customer_required", "اختر العميل لاستبدال نقاطه");
            if (req.RedeemPoints < settings.LoyaltyMinRedeem)
                throw AppException.Bad("points_below_min", $"الحد الأدنى للاستبدال {settings.LoyaltyMinRedeem} نقطة");
            if (req.RedeemPoints > customer.LoyaltyPoints)
                throw AppException.Bad("insufficient_points", $"رصيد العميل {customer.LoyaltyPoints} نقطة فقط");
            pointsDiscount = Pricing.Round(req.RedeemPoints * settings.LoyaltyPointValue);
        }

        var totalDiscount = couponDiscount + manualDiscount + pointsDiscount;
        if (totalDiscount > gross)
            throw AppException.Bad("discount_exceeds_total", "مجموع الخصومات أكبر من قيمة الفاتورة");

        var price = Pricing.Calculate(gross, totalDiscount, settings.VatEnabled, settings.VatRate, settings.PricesIncludeVat);

        // Commission base: the services' share of the discounted net, excluding VAT and retail products
        var serviceGross = items.Where(i => i.ItemType == SaleItemType.Service).Sum(i => i.LineTotal);
        var serviceNet = gross == 0 ? 0m : serviceGross * (gross - price.Discount) / gross;
        var commissionBase = Pricing.ExcludeVat(serviceNet, settings.VatEnabled, settings.VatRate, settings.PricesIncludeVat);

        var pointsEarned = settings.LoyaltyEnabled && customer is not null
            ? (int)Math.Floor(price.Total * settings.LoyaltyPointsPerSar)
            : 0;

        var vatNumber = settings.VatEnabled ? settings.VatNumber : null;
        if (settings.VatEnabled && !Pricing.IsValidVatNumber(vatNumber))
            throw AppException.Bad("vat_number_missing", "الرقم الضريبي غير مكتمل في الإعدادات — لا يمكن إصدار فاتورة ضريبية");

        var now = DateTime.UtcNow;

        // Transactional: sale + items + cash movement + stock + loyalty all commit together
        await using var tx = await db.BeginTransactionAsync(ct);

        var receiptNo = await NextReceiptNoAsync(user.OrganizationId, ct);
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            BarberId = barber.Id,
            CustomerId = customer?.Id,
            CreatedBy = user.ProfileId,
            ReceiptNo = receiptNo,
            PaymentMethod = req.PaymentMethod,
            Status = SaleStatus.Completed,
            GrossAmount = price.Gross,
            DiscountAmount = price.Discount,
            SubtotalAmount = price.Subtotal,
            VatRate = price.VatRate,
            VatAmount = price.Vat,
            TotalAmount = price.Total,
            CommissionBase = commissionBase,
            CouponId = coupon?.Id,
            CouponCode = coupon?.Code,
            PointsRedeemed = req.RedeemPoints,
            PointsEarned = pointsEarned,
            AppointmentId = appointment?.Id,
            InvoiceVatNumber = vatNumber,
            InvoiceSellerName = settings.VatEnabled
                ? (string.IsNullOrWhiteSpace(settings.LegalName)
                    ? await db.Organizations.Where(o => o.Id == user.OrganizationId).Select(o => o.Name).FirstAsync(ct)
                    : settings.LegalName)
                : null,
            IdempotencyKey = string.IsNullOrWhiteSpace(req.IdempotencyKey) ? null : req.IdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items
        };
        items.ForEach(i => i.SaleId = sale.Id);
        db.Sales.Add(sale);

        // stock out
        foreach (var (productId, qty) in productQty)
        {
            var stock = stockRows[productId];
            stock.Quantity -= qty;
            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BranchId = branch.Id,
                ProductId = productId,
                Type = StockMovementType.Sale,
                Quantity = -qty,
                BalanceAfter = stock.Quantity,
                SaleId = sale.Id,
                Note = $"بيع #{receiptNo}",
                CreatedBy = user.ProfileId,
                CreatedAt = now
            });
        }

        if (coupon is not null) coupon.UsedCount += 1;

        if (customer is not null && (req.RedeemPoints > 0 || pointsEarned > 0))
        {
            if (req.RedeemPoints > 0)
            {
                customer.LoyaltyPoints -= req.RedeemPoints;
                db.LoyaltyTransactions.Add(LoyaltyTx(user, customer, LoyaltyTxType.Redeem, -req.RedeemPoints, sale.Id, $"استبدال في بيع #{receiptNo}", now));
            }
            if (pointsEarned > 0)
            {
                customer.LoyaltyPoints += pointsEarned;
                db.LoyaltyTransactions.Add(LoyaltyTx(user, customer, LoyaltyTxType.Earn, pointsEarned, sale.Id, $"نقاط بيع #{receiptNo}", now));
            }
            customer.UpdatedAt = now;
        }

        if (appointment is not null)
        {
            appointment.Status = AppointmentStatus.Completed;
            appointment.SaleId = sale.Id;
            appointment.BarberId ??= barber.Id;
            appointment.CustomerId ??= customer?.Id;
            appointment.UpdatedAt = now;
        }

        Guid? shiftId = null;
        if (req.PaymentMethod == PaymentMethod.Cash)
        {
            var shift = await EnsureOpenShiftAsync(user, branch, ct);
            shiftId = shift.Id;
            db.CashMovements.Add(new CashMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BranchId = branch.Id,
                ShiftId = shift.Id,
                Type = CashMovementType.Sale,
                Amount = price.Total,
                SaleId = sale.Id,
                Description = $"بيع نقدي #{receiptNo}",
                CreatedBy = user.ProfileId,
                CreatedAt = now
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "sale.created",
            EntityType = nameof(Sale),
            EntityId = sale.Id.ToString(),
            Details = $"branch={branch.Name}; barber={barber.FullName}; total={price.Total}; discount={price.Discount}; vat={price.Vat}; payment={req.PaymentMethod}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var saved = await db.Sales.Include(x => x.Items).FirstAsync(x => x.Id == sale.Id, ct);
        return new { replayed = false, sale = await MapSaleAsync(saved, ct), shiftId };
    }

    public async Task CancelAsync(CurrentUser user, Guid saleId, string reason, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleVoid))
            throw AppException.Forbidden("ليس لديك صلاحية لإلغاء العمليات");

        var sale = await db.Sales.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == saleId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("العملية غير موجودة");

        if (sale.Status == SaleStatus.Cancelled)
            throw AppException.Conflict("already_cancelled", "العملية ملغاة مسبقًا");

        if (!Permissions.CanAccessBranch(user, sale.BranchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");

        if (string.IsNullOrWhiteSpace(reason))
            throw AppException.Bad("reason_required", "اكتب سبب الإلغاء");

        var now = DateTime.UtcNow;
        await using var tx = await db.BeginTransactionAsync(ct);

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAt = now;
        sale.CancelReason = reason.Trim();
        sale.CancelledBy = user.ProfileId;
        sale.UpdatedAt = now;

        // Reverse cash movement only, keeping the original sale intact
        if (sale.PaymentMethod == PaymentMethod.Cash)
        {
            var openShift = await db.Shifts.Where(s => s.BranchId == sale.BranchId && s.Status == ShiftStatus.Open)
                .OrderByDescending(s => s.OpenedAt).FirstOrDefaultAsync(ct);
            if (openShift is not null)
            {
                db.CashMovements.Add(new CashMovement
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = user.OrganizationId,
                    BranchId = sale.BranchId,
                    ShiftId = openShift.Id,
                    Type = CashMovementType.SaleVoid,
                    Amount = sale.TotalAmount,
                    SaleId = sale.Id,
                    Description = $"إلغاء بيع #{sale.ReceiptNo}: {reason.Trim()}",
                    CreatedBy = user.ProfileId,
                    CreatedAt = now
                });
            }
        }

        // Return sold products to the branch stock
        foreach (var line in sale.Items.Where(i => i.ItemType == SaleItemType.Product && i.ProductId is not null))
        {
            var stock = await db.ProductStocks.FirstOrDefaultAsync(s => s.ProductId == line.ProductId && s.BranchId == sale.BranchId, ct);
            if (stock is null)
            {
                stock = new ProductStock { Id = Guid.NewGuid(), ProductId = line.ProductId!.Value, BranchId = sale.BranchId };
                db.ProductStocks.Add(stock);
            }
            stock.Quantity += line.Quantity;
            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BranchId = sale.BranchId,
                ProductId = line.ProductId!.Value,
                Type = StockMovementType.SaleVoid,
                Quantity = line.Quantity,
                BalanceAfter = stock.Quantity,
                SaleId = sale.Id,
                Note = $"إلغاء بيع #{sale.ReceiptNo}",
                CreatedBy = user.ProfileId,
                CreatedAt = now
            });
        }

        if (sale.CouponId is not null)
        {
            var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == sale.CouponId, ct);
            if (coupon is not null && coupon.UsedCount > 0) coupon.UsedCount -= 1;
        }

        // Reverse loyalty: take back earned points, give back redeemed ones
        if (sale.CustomerId is not null && (sale.PointsEarned > 0 || sale.PointsRedeemed > 0))
        {
            var customer = await db.Customers.FirstAsync(c => c.Id == sale.CustomerId, ct);
            var delta = sale.PointsRedeemed - sale.PointsEarned;
            customer.LoyaltyPoints += delta;
            customer.UpdatedAt = now;
            db.LoyaltyTransactions.Add(LoyaltyTx(user, customer, LoyaltyTxType.Reverse, delta, sale.Id, $"عكس نقاط بيع ملغى #{sale.ReceiptNo}", now));
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            ActorId = user.ProfileId,
            Action = "sale.cancelled",
            EntityType = nameof(Sale),
            EntityId = sale.Id.ToString(),
            Details = $"reason={reason.Trim()}; amount={sale.TotalAmount}",
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static LoyaltyTransaction LoyaltyTx(CurrentUser user, Customer customer, LoyaltyTxType type, int points, Guid saleId, string note, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            CustomerId = customer.Id,
            Type = type,
            Points = points,
            BalanceAfter = customer.LoyaltyPoints,
            SaleId = saleId,
            Note = note,
            CreatedBy = user.ProfileId,
            CreatedAt = now
        };

    public async Task<object> GetAsync(CurrentUser user, Guid saleId, CancellationToken ct = default)
    {
        var sale = await db.Sales.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == saleId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("العملية غير موجودة");
        if (user.Role == UserRole.Barber)
        {
            var linkedBarberId = await db.Barbers.AsNoTracking()
                .Where(b => b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
            if (sale.BarberId != linkedBarberId)
                throw AppException.Forbidden("لا تملك صلاحية على هذه العملية");
        }
        else if (!Permissions.CanAccessBranch(user, sale.BranchId))
        {
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        }
        return await MapSaleAsync(sale, ct);
    }

    public async Task<object> ListAsync(CurrentUser user, Guid? branchId, DateTime? from, DateTime? to, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.SaleView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض المبيعات");

        Guid? linkedBarberId = null;
        if (user.Role == UserRole.Barber)
        {
            linkedBarberId = await db.Barbers.AsNoTracking()
                .Where(b => b.LinkedProfileId == user.ProfileId && b.OrganizationId == user.OrganizationId)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        }

        IQueryable<Sale> query;
        if (linkedBarberId is not null)
        {
            // A barber sees only his own sales
            query = db.Sales.AsNoTracking().Where(x => x.BarberId == linkedBarberId.Value);
        }
        else
        {
            var allowed = Permissions.AccessesAllBranches(user)
                ? db.Branches.Where(x => x.OrganizationId == user.OrganizationId).Select(x => x.Id)
                : user.BranchIds.AsQueryable();
            query = db.Sales.AsNoTracking()
                .Where(x => x.OrganizationId == user.OrganizationId && allowed.Contains(x.BranchId));
        }

        if (branchId.HasValue)
        {
            if (!Permissions.CanAccessBranch(user, branchId.Value))
                throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            query = query.Where(x => x.BranchId == branchId.Value);
        }
        if (from.HasValue || to.HasValue)
        {
            // whole local calendar days, both ends inclusive
            var range = await clock.RangeAsync(user.OrganizationId, from, to, DateRange.MaxDays, ct);
            query = query.Where(x => x.CreatedAt >= range.FromUtc && x.CreatedAt < range.ToUtc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.ReceiptNo.ToString() == s ||
                (x.CustomerId != null && (
                    db.Customers.Any(c => c.Id == x.CustomerId && c.FullName.Contains(s)) ||
                    db.Customers.Any(c => c.Id == x.CustomerId && c.Phone != null && c.Phone.Contains(s)))));
        }

        var totalCount = await query.CountAsync(ct);
        var sales = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ReceiptNo,
                x.CreatedAt,
                BranchName = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).First(),
                BarberName = db.Barbers.Where(b => b.Id == x.BarberId).Select(b => b.FullName).First(),
                CustomerName = x.CustomerId != null
                    ? db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.FullName).First()
                    : "عميل نقدي / بدون تسجيل",
                x.PaymentMethod,
                PaymentText = x.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
                x.Status,
                StatusText = x.Status == SaleStatus.Completed ? "مكتملة" : "ملغاة",
                x.TotalAmount,
                ServicesText = string.Join(" + ", x.Items.Select(i => i.ServiceName + (i.Quantity > 1 ? $" ×{i.Quantity}" : "")))
            }).ToListAsync(ct);

        return new { totalCount, page, pageSize, items = sales };
    }

    private async Task<object> MapSaleAsync(Sale sale, CancellationToken ct)
    {
        var branch = await db.Branches.Where(b => b.Id == sale.BranchId).Select(b => new { b.Name, b.Address, b.Phone }).FirstAsync(ct);
        var barberName = await db.Barbers.Where(b => b.Id == sale.BarberId).Select(b => b.FullName).FirstAsync(ct);
        var customer = sale.CustomerId is null
            ? null
            : await db.Customers.Where(c => c.Id == sale.CustomerId).Select(c => new { c.FullName, c.Phone, c.LoyaltyPoints }).FirstAsync(ct);
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == sale.OrganizationId)
            .Select(o => new { o.Name, o.LogoUrl }).FirstAsync(ct);
        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == sale.OrganizationId, ct);

        var isTaxInvoice = sale.InvoiceVatNumber is not null;
        return new
        {
            sale.Id,
            sale.ReceiptNo,
            sale.CreatedAt,
            sale.BranchId,
            BranchName = branch.Name,
            sale.BarberId,
            BarberName = barberName,
            sale.CustomerId,
            CustomerName = customer?.FullName ?? "عميل نقدي / بدون تسجيل",
            CustomerPhone = customer?.Phone,
            CustomerPoints = customer?.LoyaltyPoints,
            sale.PaymentMethod,
            PaymentText = sale.PaymentMethod == PaymentMethod.Cash ? "نقدي" : "بطاقة",
            sale.Status,
            StatusText = sale.Status == SaleStatus.Completed ? "مكتملة" : "ملغاة",
            sale.CancelReason,
            GrossAmount = sale.GrossAmount == 0 ? sale.TotalAmount : sale.GrossAmount,
            sale.DiscountAmount,
            SubtotalAmount = sale.SubtotalAmount == 0 ? sale.TotalAmount - sale.VatAmount : sale.SubtotalAmount,
            sale.VatRate,
            sale.VatAmount,
            sale.TotalAmount,
            sale.CouponCode,
            sale.PointsRedeemed,
            sale.PointsEarned,
            sale.AppointmentId,
            IsTaxInvoice = isTaxInvoice,
            Invoice = new
            {
                Title = isTaxInvoice ? "فاتورة ضريبية مبسطة" : "إيصال",
                SellerName = sale.InvoiceSellerName ?? org.Name,
                OrganizationName = org.Name,
                org.LogoUrl,
                VatNumber = sale.InvoiceVatNumber,
                settings?.CommercialRegistration,
                Address = settings?.TaxAddress ?? branch.Address,
                Phone = settings?.ReceiptPhone ?? branch.Phone,
                Footer = settings?.ReceiptFooter,
                ZatcaQr = isTaxInvoice
                    ? ZatcaQr.Encode(sale.InvoiceSellerName!, sale.InvoiceVatNumber!, sale.CreatedAt, sale.TotalAmount, sale.VatAmount)
                    : null
            },
            Items = sale.Items.Select(i => new
            {
                i.ItemType,
                i.ServiceId,
                i.ProductId,
                i.ServiceName,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal
            }).ToList()
        };
    }

    private async Task<int> NextReceiptNoAsync(Guid orgId, CancellationToken ct)
    {
        var max = await db.Sales.Where(x => x.OrganizationId == orgId).MaxAsync(x => (int?)x.ReceiptNo, ct) ?? 1000;
        return max + 1;
    }

    /// <summary>Ensures an open shift for cash sales; auto-opens with the drawer's known balance.</summary>
    private async Task<Shift> EnsureOpenShiftAsync(CurrentUser user, Branch branch, CancellationToken ct)
    {
        var open = await db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branch.Id && s.Status == ShiftStatus.Open, ct);
        if (open is not null) return open;

        var drawer = await db.CashDrawers.FirstOrDefaultAsync(d => d.BranchId == branch.Id, ct);
        if (drawer is null)
        {
            drawer = new CashDrawer { Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, BranchId = branch.Id, CreatedAt = DateTime.UtcNow };
            db.CashDrawers.Add(drawer);
            await db.SaveChangesAsync(ct);
        }

        // Opening amount = last closed shift's counted cash (money assumed still in drawer), else 0
        var lastCounted = await db.Shifts
            .Where(s => s.BranchId == branch.Id && s.Status == ShiftStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .Select(s => s.CountedCash)
            .FirstOrDefaultAsync(ct) ?? 0m;

        var now = DateTime.UtcNow;
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            DrawerId = drawer.Id,
            OpenedBy = user.ProfileId,
            OpeningCash = lastCounted,
            OpenedAt = now,
            Status = ShiftStatus.Open,
            AutoOpened = true
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return shift;
    }
}
