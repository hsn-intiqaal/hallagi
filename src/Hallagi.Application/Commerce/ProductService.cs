namespace Hallagi.Application.Commerce;

public class ProductService(IAppDbContext db)
{
    public async Task<object> ListAsync(CurrentUser user, Guid? branchId, string? search, bool lowStockOnly, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ProductView) && !Permissions.Can(user, Permissions.ProductManage))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض المنتجات");
        if (branchId is not null && !Permissions.CanAccessBranch(user, branchId.Value))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");

        var query = db.Products.AsNoTracking().Where(p => p.OrganizationId == user.OrganizationId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s) || (p.Sku != null && p.Sku.Contains(s)) || (p.Barcode != null && p.Barcode == s));
        }
        var products = await query.OrderBy(p => p.Name).Include(p => p.Stock).ToListAsync(ct);

        var visibleBranches = Permissions.AccessesAllBranches(user)
            ? await db.Branches.Where(b => b.OrganizationId == user.OrganizationId).Select(b => new { b.Id, b.Name }).ToListAsync(ct)
            : await db.Branches.Where(b => user.BranchIds.Contains(b.Id)).Select(b => new { b.Id, b.Name }).ToListAsync(ct);
        var branchIds = branchId is null ? visibleBranches.Select(b => b.Id).ToHashSet() : new HashSet<Guid> { branchId.Value };

        var items = products.Select(p =>
        {
            var stock = p.Stock.Where(s => branchIds.Contains(s.BranchId)).ToList();
            var qty = stock.Sum(s => s.Quantity);
            var low = stock.Any(s => s.Quantity <= s.LowStockThreshold) || stock.Count == 0;
            return new
            {
                p.Id, p.Name, p.Sku, p.Barcode, p.Price, p.Cost, p.Status,
                StatusText = p.Status == EntityStatus.Active ? "نشط" : "موقوف",
                Margin = p.Price == 0 ? 0 : Math.Round((p.Price - p.Cost) / p.Price * 100, 1),
                Quantity = qty,
                LowStock = low,
                Branches = visibleBranches.Where(b => branchIds.Contains(b.Id)).Select(b =>
                {
                    var row = stock.FirstOrDefault(s => s.BranchId == b.Id);
                    return new { BranchId = b.Id, BranchName = b.Name, Quantity = row?.Quantity ?? 0, LowStockThreshold = row?.LowStockThreshold ?? 5 };
                }).ToList()
            };
        }).Where(x => !lowStockOnly || x.LowStock).ToList();

        return new
        {
            items,
            summary = new
            {
                count = items.Count,
                lowStockCount = items.Count(i => i.LowStock && i.Status == EntityStatus.Active),
                stockValue = items.Sum(i => i.Quantity * i.Cost),
                retailValue = items.Sum(i => i.Quantity * i.Price)
            }
        };
    }

    public async Task<Guid> CreateAsync(CurrentUser user, ProductInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ProductManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة المنتجات");
        Validate(input);
        await EnsureUniqueBarcodeAsync(user, input.Barcode, null, ct);
        var now = DateTime.UtcNow;
        var p = new Product { Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, CreatedAt = now };
        Apply(p, input, now);
        db.Products.Add(p);
        db.AuditLogs.Add(Audit.Entry(user, "product.created", nameof(Product), p.Id, $"name={p.Name}; price={p.Price}", now));
        await db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task UpdateAsync(CurrentUser user, Guid id, ProductInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.ProductManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة المنتجات");
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("المنتج غير موجود");
        Validate(input);
        await EnsureUniqueBarcodeAsync(user, input.Barcode, id, ct);
        var now = DateTime.UtcNow;
        Apply(p, input, now);
        db.AuditLogs.Add(Audit.Entry(user, "product.updated", nameof(Product), p.Id, $"name={p.Name}; price={p.Price}", now));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Receives goods (Purchase) or corrects a count (Adjustment, signed quantity).</summary>
    public async Task<object> AdjustStockAsync(CurrentUser user, Guid productId, StockAdjustInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.InventoryManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة المخزون");
        if (!Permissions.CanAccessBranch(user, input.BranchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
        if (input.Type is not (StockMovementType.Purchase or StockMovementType.Adjustment))
            throw AppException.Bad("invalid_type", "نوع الحركة غير مدعوم");
        if (input.Type == StockMovementType.Purchase && input.Quantity <= 0)
            throw AppException.Bad("invalid_quantity", "كمية الاستلام يجب أن تكون أكبر من صفر");
        if (input.Quantity == 0 && input.LowStockThreshold is null)
            throw AppException.Bad("invalid_quantity", "أدخل الكمية");
        if (Math.Abs(input.Quantity) > 100_000)
            throw AppException.Bad("invalid_quantity", "الكمية غير صحيحة");

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("المنتج غير موجود");
        if (!await db.Branches.AnyAsync(b => b.Id == input.BranchId && b.OrganizationId == user.OrganizationId, ct))
            throw AppException.Bad("invalid_branch", "الفرع غير موجود");

        var stock = await db.ProductStocks.FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == input.BranchId, ct);
        if (stock is null)
        {
            stock = new ProductStock { Id = Guid.NewGuid(), ProductId = productId, BranchId = input.BranchId };
            db.ProductStocks.Add(stock);
        }
        if (stock.Quantity + input.Quantity < 0)
            throw AppException.Bad("negative_stock", $"لا يمكن أن يصبح المخزون سالبًا (المتوفر {stock.Quantity})");
        if (input.LowStockThreshold is not null)
        {
            if (input.LowStockThreshold < 0) throw AppException.Bad("invalid_threshold", "حد التنبيه غير صحيح");
            stock.LowStockThreshold = input.LowStockThreshold.Value;
        }

        var now = DateTime.UtcNow;
        if (input.Quantity != 0)
        {
            stock.Quantity += input.Quantity;
            if (input.Type == StockMovementType.Purchase && input.UnitCost is > 0)
                product.Cost = input.UnitCost.Value;
            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = user.OrganizationId,
                BranchId = input.BranchId,
                ProductId = productId,
                Type = input.Type,
                Quantity = input.Quantity,
                BalanceAfter = stock.Quantity,
                UnitCost = input.UnitCost,
                Note = input.Note?.Trim(),
                CreatedBy = user.ProfileId,
                CreatedAt = now
            });
            db.AuditLogs.Add(Audit.Entry(user, "stock." + input.Type.ToString().ToLowerInvariant(), nameof(Product), productId,
                $"branch={input.BranchId}; qty={input.Quantity}; balance={stock.Quantity}", now));
        }
        await db.SaveChangesAsync(ct);
        return new { quantity = stock.Quantity, stock.LowStockThreshold };
    }

    public async Task<object> MovementsAsync(CurrentUser user, Guid productId, Guid? branchId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.InventoryManage) && !Permissions.Can(user, Permissions.ProductManage))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض حركة المخزون");
        var query = db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId && m.OrganizationId == user.OrganizationId);
        if (branchId is not null) query = query.Where(m => m.BranchId == branchId);
        else if (!Permissions.AccessesAllBranches(user)) query = query.Where(m => user.BranchIds.Contains(m.BranchId));

        var items = await query.OrderByDescending(m => m.CreatedAt).Take(100)
            .Select(m => new
            {
                m.Id, m.CreatedAt, m.Type, m.Quantity, m.BalanceAfter, m.UnitCost, m.Note,
                BranchName = db.Branches.Where(b => b.Id == m.BranchId).Select(b => b.Name).First(),
                ByName = db.Profiles.Where(p => p.Id == m.CreatedBy).Select(p => p.FullName).FirstOrDefault()
            }).ToListAsync(ct);
        return new
        {
            items = items.Select(m => new
            {
                m.Id, m.CreatedAt, m.Type, m.Quantity, m.BalanceAfter, m.UnitCost, m.Note, m.BranchName, m.ByName,
                TypeText = m.Type switch
                {
                    StockMovementType.Purchase => "استلام",
                    StockMovementType.Sale => "بيع",
                    StockMovementType.SaleVoid => "إرجاع (إلغاء بيع)",
                    _ => "تسوية جرد"
                }
            })
        };
    }

    private static void Validate(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw AppException.Bad("name_required", "اسم المنتج مطلوب");
        if (input.Price < 0 || input.Price > 100_000) throw AppException.Bad("invalid_price", "سعر البيع غير صحيح");
        if (input.Cost < 0 || input.Cost > 100_000) throw AppException.Bad("invalid_cost", "سعر التكلفة غير صحيح");
    }

    private async Task EnsureUniqueBarcodeAsync(CurrentUser user, string? barcode, Guid? exceptId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        var b = barcode.Trim();
        if (await db.Products.AnyAsync(p => p.OrganizationId == user.OrganizationId && p.Barcode == b && p.Id != exceptId, ct))
            throw AppException.Conflict("duplicate_barcode", "الباركود مستخدم لمنتج آخر");
    }

    private static void Apply(Product p, ProductInput input, DateTime now)
    {
        p.Name = input.Name.Trim();
        p.Sku = string.IsNullOrWhiteSpace(input.Sku) ? null : input.Sku.Trim();
        p.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
        p.Price = input.Price;
        p.Cost = input.Cost;
        p.Status = input.Active ? EntityStatus.Active : EntityStatus.Inactive;
        p.UpdatedAt = now;
    }
}
