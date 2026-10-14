using Hallagi.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Hallagi.Api.Services;

namespace Hallagi.Api.Data;

public static class DbSeeder
{
    public const string DemoPassword = "Hallagi@123"; // dev/demo only; never use in production
    public const string SuperAdminEmail = "admin@hallagi.sa"; // حساب مالك المنصة (عرض توضيحي)

    public static async Task SeedAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        // Platform owner account (highest level) — created once, demo credentials in dev
        if (!await db.Profiles.AnyAsync(p => p.IsSuperAdmin, ct))
        {
            var admin = new Profile
            {
                Id = Guid.NewGuid(),
                FullName = "مالك منصة حلاقي",
                Email = SuperAdminEmail,
                Phone = "0550000000",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
                Status = EntityStatus.Active,
                IsSuperAdmin = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Profiles.Add(admin);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Organizations.AnyAsync(ct))
        {
            await SeedDemoOrganizationAsync(db, ct);
        }

        // always ensure SaaS plans/subscriptions/devices exist for every organization
        await SeedSaaSAsync(db, ct);
    }

    /// <summary>Seeds the full demo organization: services, barbers, customers, sales, expenses.</summary>
    private static async Task SeedDemoOrganizationAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var localToday = DateTime.Today; // server local day used for demo sales window

        // ===== Organization + branches =====
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "صالون النخبة للحلاقة",
            Currency = "SAR",
            TimeZone = "Asia/Riyadh",
            CreatedAt = now,
            UpdatedAt = now,
            Settings = new OrganizationSettings
            {
                EnableCash = true,
                EnableCard = true,
                ReceiptFooter = "شكرًا لزيارتكم — حلاقي، محلك في يدك",
                ReceiptPhone = "0112345678"
            }
        };
        db.Organizations.Add(org);

        var branches = new[]
        {
            new Branch { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "فرع النرجس", City = "الرياض", Address = "حي النرجس، شارع الأمير سلطان" },
            new Branch { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "فرع الياسمين", City = "الرياض", Address = "حي الياسمين، شارع الملك عبدالله" },
            new Branch { Id = Guid.NewGuid(), OrganizationId = org.Id, Name = "فرع الروضة", City = "الرياض", Address = "حي الروضة، طريق الملك فهد" }
        };
        foreach (var br in branches)
        {
            br.Status = EntityStatus.Active;
            br.CreatedAt = now;
            br.UpdatedAt = now;
            br.Drawer = new CashDrawer { OrganizationId = org.Id, CreatedAt = now };
            db.Branches.Add(br);
        }

        // ===== Services =====
        var services = new (string Name, decimal Price)[]
        {
            ("حلاقة شعر", 25m),
            ("حلاقة ذقن", 15m),
            ("شعر وذقن", 35m),
            ("تنظيف بشرة", 30m),
            ("صبغة شعر", 50m),
            ("حلاقة أطفال", 20m)
        };
        foreach (var (name, price) in services)
        {
            db.Services.Add(new Service
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = name,
                BasePrice = price,
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Branch-specific price override example (proves per-branch pricing works)
        var dyeServiceId = await db.Services.Where(s => s.Name == "صبغة شعر").Select(s => s.Id).FirstOrDefaultAsync(ct);
        if (dyeServiceId != Guid.Empty)
        {
            db.BranchServicePrices.Add(new BranchServicePrice
            {
                Id = Guid.NewGuid(),
                ServiceId = dyeServiceId,
                BranchId = branches[2].Id, // فرع الروضة
                Price = 55m
            });
        }

        await db.SaveChangesAsync(ct);

        // ===== Barbers (4 compensation models) =====
        var barbers = new[]
        {
            new Barber { Id = Guid.NewGuid(), OrganizationId = org.Id, FullName = "أحمد العتيبي", Phone = "0551000001", StartDate = new DateOnly(2022, 3, 1) },
            new Barber { Id = Guid.NewGuid(), OrganizationId = org.Id, FullName = "محمد القحطاني", Phone = "0551000002", StartDate = new DateOnly(2023, 7, 15) },
            new Barber { Id = Guid.NewGuid(), OrganizationId = org.Id, FullName = "خالد الشهري", Phone = "0551000003", StartDate = new DateOnly(2021, 11, 20) },
            new Barber { Id = Guid.NewGuid(), OrganizationId = org.Id, FullName = "يوسف المصري", Phone = "0551000004", StartDate = new DateOnly(2024, 1, 10) }
        };
        foreach (var barber in barbers)
        {
            barber.Status = EntityStatus.Active;
            barber.CreatedAt = now;
            barber.UpdatedAt = now;
            db.Barbers.Add(barber);
        }
        await db.SaveChangesAsync(ct);

        db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barbers[0].Id, BranchId = branches[0].Id });
        db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barbers[1].Id, BranchId = branches[1].Id });
        db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barbers[2].Id, BranchId = branches[2].Id });
        db.BarberBranchAssignments.Add(new BarberBranchAssignment { Id = Guid.NewGuid(), BarberId = barbers[3].Id, BranchId = branches[0].Id });

        var comps = new[]
        {
            // أحمد: راتب ثابت
            new BarberCompensation { Type = CompensationType.FixedSalary, MonthlySalary = 3500m },
            // محمد: عمولة 30%
            new BarberCompensation { Type = CompensationType.Commission, CommissionRate = 0.30m },
            // خالد: راتب ثابت + عمولة 15%
            new BarberCompensation { Type = CompensationType.FixedPlusCommission, MonthlySalary = 3000m, CommissionRate = 0.15m },
            // يوسف: تأجير كرسي
            new BarberCompensation { Type = CompensationType.ChairRental, ChairRentAmount = 1200m, Notes = "إيجار كرسي شهري بموجب اتفاق خاص" }
        };
        for (int i = 0; i < comps.Length; i++)
        {
            comps[i].Id = Guid.NewGuid();
            comps[i].BarberId = barbers[i].Id;
            comps[i].EffectiveFrom = new DateOnly(2024, 1, 1);
            comps[i].Status = EntityStatus.Active;
            comps[i].CreatedAt = now;
            db.BarberCompensations.Add(comps[i]);
        }

        // ===== Customers =====
        var customers = new[]
        {
            "عبدالله الدوسري", "فهد المطيري", "سعد الحربي", "ناصر الشمري", "تركي العتيبي"
        };
        foreach (var name in customers)
        {
            db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                FullName = name,
                Phone = "05" + (50000000 + Math.Abs(name.GetHashCode()) % 9999999).ToString(),
                Notes = "عميل دائم",
                CreatedAt = now.AddDays(-90),
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        // ===== Demo users (dev only) =====
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        async Task<Guid> MakeUser(string name, string email, UserRole role, int branchIndex)
        {
            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                FullName = name,
                Email = email,
                Phone = "055000000" + (branchIndex + 1),
                PasswordHash = passwordHash,
                Status = EntityStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Profiles.Add(profile);
            var member = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                ProfileId = profile.Id,
                Role = role,
                Status = MemberStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.OrganizationMembers.Add(member);

            // Owner sees all branches; others get explicit branch access.
            // A barber gets NO branch access: he only sees his own info (no store finances).
            var accessBranches = role == UserRole.Owner
                ? branches
                : role == UserRole.BranchManager
                    ? new[] { branches[0], branches[1] }
                    : role == UserRole.Barber
                        ? Array.Empty<Branch>()
                        : new[] { branches[branchIndex] };

            foreach (var br in accessBranches)
                db.MemberBranchAccess.Add(new MemberBranchAccess { Id = Guid.NewGuid(), MemberId = member.Id, BranchId = br.Id });

            if (role == UserRole.Cashier)
                member.PermissionsJson = """{"expense.create": true}"""; // demo: cashier allowed to record expenses

            await db.SaveChangesAsync(ct);
            return profile.Id;
        }

        var ownerId = await MakeUser("مالك صالون النخبة", "owner@hallagi.sa", UserRole.Owner, 0);
        await MakeUser("فيصل العبدالله (مدير فرع)", "manager@hallagi.sa", UserRole.BranchManager, 0);
        var cashierId = await MakeUser("سعيد القحطاني (كاشير)", "cashier@hallagi.sa", UserRole.Cashier, 0);
        var barberProfileId = await MakeUser("أحمد العتيبي (حلاق)", "barber@hallagi.sa", UserRole.Barber, 0);

        var barberAhmed = await db.Barbers.FirstAsync(x => x.FullName == "أحمد العتيبي", ct);
        barberAhmed.LinkedProfileId = barberProfileId;
        await db.SaveChangesAsync(ct);

        // ===== Shifts per branch (7 past closed shifts + today open) =====
        var shiftsByBranch = new Dictionary<Guid, List<Shift>>();
        foreach (var br in branches)
        {
            var list = new List<Shift>();
            for (int dayOffset = -7; dayOffset <= 0; dayOffset++)
            {
                var day = localToday.AddDays(dayOffset);
                var openedAt = new DateTime(day.Year, day.Month, day.Day, 9, 0, 0, DateTimeKind.Local).ToUniversalTime();
                var shift = new Shift
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    BranchId = br.Id,
                    DrawerId = br.Drawer!.Id,
                    OpenedBy = ownerId,
                    OpeningCash = 200m,
                    OpenedAt = openedAt,
                    Status = dayOffset == 0 ? ShiftStatus.Open : ShiftStatus.Closed,
                    AutoOpened = false
                };
                if (dayOffset < 0)
                {
                    var closedAt = openedAt.AddHours(12);
                    shift.ClosedAt = closedAt;
                    shift.ClosedBy = ownerId;
                    shift.ExpectedCash = 200m;
                    shift.CountedCash = 200m;
                    shift.Difference = 0m;
                }
                else
                {
                    shift.OpeningCash = 200m;
                }
                db.Shifts.Add(shift);
                list.Add(shift);
            }
            shiftsByBranch[br.Id] = list;
        }

        // Opening cash movement for today's shifts (drawer starts at opening amount)
        foreach (var br in branches)
        {
            var today = shiftsByBranch[br.Id].Last();
            db.CashMovements.Add(new CashMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                BranchId = br.Id,
                ShiftId = today.Id,
                Type = CashMovementType.Opening,
                Amount = today.OpeningCash,
                Description = "رصيد افتتاحي للوردية",
                CreatedBy = ownerId,
                CreatedAt = today.OpenedAt
            });
        }
        await db.SaveChangesAsync(ct);

        // ===== Sales across 8 days =====
        var allServices = await db.Services.OrderBy(x => x.Name).ToListAsync(ct);
        var allCustomers = await db.Customers.ToListAsync(ct);
        var rnd = new Random(42);
        int receipt = 1000;

        for (int dayOffset = -7; dayOffset <= 0; dayOffset++)
        {
            var day = localToday.AddDays(dayOffset);
            // fewer sales today so the dashboard shows a natural "morning of work" state
            int salesCount = dayOffset == 0 ? 6 : 9 + rnd.Next(5);

            for (int i = 0; i < salesCount; i++)
            {
                var barber = barbers[rnd.Next(barbers.Length)];
                var branchId = db.BarberBranchAssignments.Where(a => a.BarberId == barber.Id)
                    .Select(a => a.BranchId).First();

                var hour = 9 + rnd.Next(11);
                var createdAt = new DateTime(day.Year, day.Month, day.Day, hour, rnd.Next(60), 0, DateTimeKind.Local).ToUniversalTime();
                if (dayOffset == 0 && createdAt > DateTime.UtcNow.AddHours(3)) createdAt = DateTime.UtcNow.AddMinutes(-30 - i);

                // 1..3 services
                int itemCount = 1 + rnd.Next(3);
                var chosen = allServices.OrderBy(_ => rnd.Next()).Take(itemCount).ToList();
                decimal total = 0m;
                var items = new List<SaleItem>();
                foreach (var svc in chosen)
                {
                    var unitPrice = svc.BasePrice;
                    var branchPrice = await db.BranchServicePrices
                        .Where(bp => bp.ServiceId == svc.Id && bp.BranchId == branchId)
                        .Select(bp => (decimal?)bp.Price).FirstOrDefaultAsync(ct);
                    if (branchPrice.HasValue) unitPrice = branchPrice.Value;

                    var qty = rnd.Next(10) == 0 ? 2 : 1;
                    var line = unitPrice * qty;
                    total += line;
                    items.Add(new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        ServiceId = svc.Id,
                        ServiceName = svc.Name,
                        UnitPrice = unitPrice,
                        Quantity = qty,
                        LineTotal = line,
                        SaleId = Guid.Empty
                    });
                }

                // Customer: ~60% registered
                Customer? customer = rnd.Next(10) < 6 ? allCustomers[rnd.Next(allCustomers.Count)] : null;
                var method = rnd.Next(10) < 55 ? PaymentMethod.Cash : PaymentMethod.Card;

                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    BranchId = branchId,
                    BarberId = barber.Id,
                    CustomerId = customer?.Id,
                    CreatedBy = rnd.Next(10) < 8 ? cashierId : ownerId,
                    ReceiptNo = ++receipt,
                    PaymentMethod = method,
                    Status = SaleStatus.Completed,
                    TotalAmount = total,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
                db.Sales.Add(sale);
                foreach (var it in items) { it.SaleId = sale.Id; db.SaleItems.Add(it); }

                // Cash sale -> drawer movement on the shift open at that time (same day)
                if (method == PaymentMethod.Cash)
                {
                    var targetShift = shiftsByBranch[branchId][dayOffset + 7];
                    db.CashMovements.Add(new CashMovement
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = org.Id,
                        BranchId = branchId,
                        ShiftId = targetShift.Id,
                        Type = CashMovementType.Sale,
                        Amount = total,
                        SaleId = sale.Id,
                        Description = $"بيع نقدي #{sale.ReceiptNo}",
                        CreatedBy = sale.CreatedBy,
                        CreatedAt = createdAt
                    });
                }
            }
        }
        await db.SaveChangesAsync(ct);

        // Recompute expected/ counted for closed shifts (7-day window) from actual movements
        foreach (var br in branches)
        {
            foreach (var shift in shiftsByBranch[br.Id])
            {
                if (shift.Status != ShiftStatus.Closed) continue;
                var movements = await db.CashMovements.Where(m => m.ShiftId == shift.Id).ToListAsync(ct);
                var expected = shift.OpeningCash + movements.Sum(m =>
                    m.Type is CashMovementType.Sale or CashMovementType.ManualIn ? m.Amount : -m.Amount);
                shift.ExpectedCash = expected;
                shift.CountedCash = expected; // historical seed: no difference
                shift.Difference = 0m;
            }
        }
        await db.SaveChangesAsync(ct);

        // ===== Expenses =====
        var expenses = new (ExpenseCategory Cat, decimal Amount, string Desc, int BranchIdx)[]
        {
            (ExpenseCategory.Supplies, 75m, "مستلزمات حلاقة (مناشف، موس،فوم)", 0),
            (ExpenseCategory.ElectricityWater, 120m, "فاتورة كهرباء ومياه", 1),
            (ExpenseCategory.Maintenance, 90m, "صيانة كرسي حلاقة", 2)
        };
        foreach (var (cat, amount, desc, branchIdx) in expenses)
        {
            var br = branches[branchIdx];
            var expense = new Expense
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                BranchId = br.Id,
                Category = cat,
                Amount = amount,
                ExpenseDate = DateOnly.FromDateTime(localToday.AddDays(-1)),
                Description = desc,
                Source = ExpenseSource.CashDrawer,
                Status = ExpenseStatus.Active,
                CreatedBy = ownerId,
                CreatedAt = now.AddDays(-1)
            };
            db.Expenses.Add(expense);
            var openShift = shiftsByBranch[br.Id].Last();
            db.CashMovements.Add(new CashMovement
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                BranchId = br.Id,
                ShiftId = openShift.Id,
                Type = CashMovementType.Expense,
                Amount = amount,
                ExpenseId = expense.Id,
                Description = $"مصروف: {desc}",
                CreatedBy = ownerId,
                CreatedAt = now.AddDays(-1)
            });
        }
        await db.SaveChangesAsync(ct);

        // ===== Audit entry =====
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            ActorId = ownerId,
            Action = "seed.completed",
            EntityType = nameof(Organization),
            EntityId = org.Id.ToString(),
            Details = "بيانات تجريبية واقعية (صالون النخبة للحلاقة)",
            CreatedAt = now
        });

        // SaaS subscription/devices are seeded in SeedSaaSAsync
    }

    /// <summary>Seeds SaaS plans, subscriptions and demo devices (idempotent, safe to re-run).</summary>
    private static async Task SeedSaaSAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        if (!await db.SubscriptionPlans.AnyAsync(ct))
        {
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(), Name = "تجريبي", Description = "فرع واحد ومستخدمان — للتجربة",
                    MaxBranches = 1, MaxUsers = 2, MaxBarbers = 3, MonthlyPrice = 0m, CreatedAt = now
                },
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(), Name = "أساسي", Description = "٣ فروع و٥ مستخدمين مع التقارير",
                    MaxBranches = 3, MaxUsers = 5, MaxBarbers = 10,
                    AllowAdvancedReports = true, MonthlyPrice = 149m, CreatedAt = now
                },
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(), Name = "احترافي", Description = "فروع ومستخدمون غير محدودين مع التسويات المالية",
                    MaxBranches = 100, MaxUsers = 100, MaxBarbers = 200,
                    AllowAdvancedReports = true, AllowSettlements = true, MonthlyPrice = 349m, CreatedAt = now
                });
            await db.SaveChangesAsync(ct);
        }

        // Give every existing organization a subscription if missing
        var orgs = await db.Organizations.Select(o => o.Id).ToListAsync(ct);
        var proPlan = await db.SubscriptionPlans.FirstAsync(p => p.Name == "احترافي", ct);
        foreach (var orgId in orgs)
        {
            if (await db.Subscriptions.AnyAsync(s => s.OrganizationId == orgId, ct)) continue;
            db.Subscriptions.Add(new Subscription
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                PlanId = proPlan.Id,
                Status = "Active",
                PeriodStart = DateOnly.FromDateTime(DateTime.Today),
                PeriodEnd = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)),
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);

        // One demo POS device per organization's first branch
        foreach (var orgId in orgs)
        {
            if (await db.Devices.AnyAsync(d => d.OrganizationId == orgId, ct)) continue;
            var firstBranch = await db.Branches.Where(b => b.OrganizationId == orgId).OrderBy(b => b.CreatedAt).FirstAsync(ct);
            db.Devices.Add(new Device
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                BranchId = firstBranch.Id,
                Name = "كاشير 1",
                DeviceCode = "POS-001",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
