using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Hallagi.Application.Booking;

public partial class AppointmentService(IAppDbContext db)
{
    /// <summary>Statuses that occupy a barber's chair time.</summary>
    private static readonly AppointmentStatus[] Blocking =
        { AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.InService };

    private const int MaxActiveOnlineBookingsPerPhone = 3;

    // ======================= Staff API =======================

    public async Task<object> DayAsync(CurrentUser user, Guid branchId, DateOnly? date, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AppointmentView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض المواعيد");
        await EnsureBranchAccessAsync(user, branchId, ct);

        var (tz, settings) = await OrgContextAsync(user.OrganizationId, ct);
        var day = date ?? OrgTime.Today(tz);
        var (fromUtc, toUtc) = DayRangeUtc(day, tz);

        var branch = await db.Branches.AsNoTracking().FirstAsync(b => b.Id == branchId, ct);
        var barbers = await db.Barbers.AsNoTracking()
            .Where(b => b.OrganizationId == user.OrganizationId && b.Status == EntityStatus.Active
                        && b.BranchAssignments.Any(a => a.BranchId == branchId))
            .OrderBy(b => b.FullName).Select(b => new { b.Id, b.FullName, b.LinkedProfileId }).ToListAsync(ct);

        var query = db.Appointments.AsNoTracking().Include(a => a.Items)
            .Where(a => a.OrganizationId == user.OrganizationId && a.BranchId == branchId
                        && a.StartAt < toUtc && a.EndAt > fromUtc);
        if (user.Role == UserRole.Barber)
        {
            var own = barbers.FirstOrDefault(b => b.LinkedProfileId == user.ProfileId)?.Id;
            query = query.Where(a => a.BarberId == own);
        }
        var appts = await query.OrderBy(a => a.StartAt).ToListAsync(ct);

        var mapped = appts.Select(a => Map(a, tz, barbers.ToDictionary(b => b.Id, b => b.FullName))).ToList();
        return new
        {
            date = day,
            branch = new { branch.Id, branch.Name, OpensAt = branch.OpensAt.ToString("HH\\:mm"), ClosesAt = branch.ClosesAt.ToString("HH\\:mm") },
            slotMinutes = settings.BookingSlotMinutes,
            barbers = barbers.Select(b => new { b.Id, b.FullName }),
            appointments = mapped.Where(a => a.Status != AppointmentStatus.Waiting),
            waitlist = mapped.Where(a => a.Status == AppointmentStatus.Waiting),
            summary = new
            {
                total = appts.Count(a => a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow)),
                completed = appts.Count(a => a.Status == AppointmentStatus.Completed),
                waiting = appts.Count(a => a.Status == AppointmentStatus.Waiting),
                noShow = appts.Count(a => a.Status == AppointmentStatus.NoShow),
                online = appts.Count(a => a.Source == AppointmentSource.Online),
                expectedRevenue = appts.Where(a => a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
                    .Sum(a => a.Items.Sum(i => i.Price))
            }
        };
    }

    public async Task<object> GetAsync(CurrentUser user, Guid id, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AppointmentView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض المواعيد");
        var a = await db.Appointments.AsNoTracking().Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الموعد غير موجود");
        await EnsureBranchAccessAsync(user, a.BranchId, ct);
        var (tz, _) = await OrgContextAsync(user.OrganizationId, ct);
        var names = await db.Barbers.AsNoTracking().Where(b => b.OrganizationId == user.OrganizationId)
            .ToDictionaryAsync(b => b.Id, b => b.FullName, ct);
        return Map(a, tz, names);
    }

    public async Task<object> CreateAsync(CurrentUser user, AppointmentInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AppointmentManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة المواعيد");
        await EnsureBranchAccessAsync(user, input.BranchId, ct);
        var (tz, settings) = await OrgContextAsync(user.OrganizationId, ct);
        var branch = await db.Branches.FirstAsync(b => b.Id == input.BranchId, ct);
        var services = await LoadServicesAsync(user.OrganizationId, branch.Id, input.ServiceIds, ct);
        var duration = Math.Max(settings.BookingSlotMinutes, services.Sum(s => s.Duration));

        if (input.BarberId is not null) await EnsureBarberInBranchAsync(user.OrganizationId, input.BarberId.Value, branch.Id, ct);

        var customer = await ResolveCustomerAsync(user.OrganizationId, input.CustomerId, input.CustomerName, input.CustomerPhone, ct);
        var now = DateTime.UtcNow;

        DateTime startUtc;
        Guid? barberId = input.BarberId;
        AppointmentStatus status;
        AppointmentSource source;
        if (input.WalkIn)
        {
            startUtc = now;
            status = AppointmentStatus.Waiting;
            source = AppointmentSource.WalkIn;
        }
        else
        {
            if (input.Date is null || string.IsNullOrWhiteSpace(input.Time))
                throw AppException.Bad("time_required", "حدد تاريخ ووقت الموعد");
            startUtc = ParseLocal(input.Date.Value, input.Time, tz);
            EnsureWithinHours(branch, startUtc, duration, tz);
            barberId = await AssignBarberAsync(user.OrganizationId, branch.Id, barberId, startUtc, startUtc.AddMinutes(duration), null, ct);
            status = AppointmentStatus.Confirmed;
            source = AppointmentSource.Staff;
        }

        var appt = new Appointment
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            BranchId = branch.Id,
            BarberId = barberId,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerPhone = customer.Phone,
            StartAt = startUtc,
            EndAt = startUtc.AddMinutes(duration),
            Status = status,
            Source = source,
            Notes = input.Notes?.Trim(),
            Reference = await NewReferenceAsync(user.OrganizationId, ct),
            CreatedBy = user.ProfileId,
            CreatedAt = now,
            UpdatedAt = now,
            Items = services.Select(s => new AppointmentItem
            {
                Id = Guid.NewGuid(), ServiceId = s.Id, ServiceName = s.Name, Price = s.Price, DurationMinutes = s.Duration
            }).ToList()
        };
        db.Appointments.Add(appt);
        db.AuditLogs.Add(Audit.Entry(user, input.WalkIn ? "appointment.walkin" : "appointment.created", nameof(Appointment), appt.Id,
            $"start={appt.StartAt:O}; barber={barberId}; customer={appt.CustomerName}", now));
        await db.SaveChangesAsync(ct);
        return new { appt.Id, appt.Reference, message = input.WalkIn ? "تمت إضافة العميل لقائمة الانتظار" : "تم حجز الموعد بنجاح" };
    }

    public async Task RescheduleAsync(CurrentUser user, Guid id, AppointmentInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AppointmentManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة المواعيد");
        var appt = await db.Appointments.Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الموعد غير موجود");
        await EnsureBranchAccessAsync(user, appt.BranchId, ct);
        if (appt.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
            throw AppException.Conflict("appointment_closed", "لا يمكن تعديل موعد مغلق");
        if (input.BranchId != appt.BranchId)
            throw AppException.Bad("branch_change", "لا يمكن نقل الموعد لفرع آخر — ألغِه واحجز من جديد");

        var (tz, settings) = await OrgContextAsync(user.OrganizationId, ct);
        var branch = await db.Branches.FirstAsync(b => b.Id == appt.BranchId, ct);
        var services = await LoadServicesAsync(user.OrganizationId, branch.Id, input.ServiceIds, ct);
        var duration = Math.Max(settings.BookingSlotMinutes, services.Sum(s => s.Duration));
        if (input.BarberId is not null) await EnsureBarberInBranchAsync(user.OrganizationId, input.BarberId.Value, branch.Id, ct);

        var startUtc = appt.StartAt;
        var barberId = input.BarberId;
        if (appt.Status != AppointmentStatus.Waiting)
        {
            if (input.Date is null || string.IsNullOrWhiteSpace(input.Time))
                throw AppException.Bad("time_required", "حدد تاريخ ووقت الموعد");
            startUtc = ParseLocal(input.Date.Value, input.Time, tz);
            EnsureWithinHours(branch, startUtc, duration, tz);
            barberId = await AssignBarberAsync(user.OrganizationId, branch.Id, barberId, startUtc, startUtc.AddMinutes(duration), appt.Id, ct);
        }

        var customer = await ResolveCustomerAsync(user.OrganizationId, input.CustomerId ?? appt.CustomerId,
            input.CustomerName ?? appt.CustomerName, input.CustomerPhone ?? appt.CustomerPhone, ct);

        var now = DateTime.UtcNow;
        appt.StartAt = startUtc;
        appt.EndAt = startUtc.AddMinutes(duration);
        appt.BarberId = barberId;
        appt.CustomerId = customer.Id;
        appt.CustomerName = customer.Name;
        appt.CustomerPhone = customer.Phone;
        appt.Notes = input.Notes?.Trim();
        appt.UpdatedAt = now;
        db.AppointmentItems.RemoveRange(appt.Items);
        foreach (var s in services)
            db.AppointmentItems.Add(new AppointmentItem
            {
                Id = Guid.NewGuid(), AppointmentId = appt.Id, ServiceId = s.Id, ServiceName = s.Name, Price = s.Price, DurationMinutes = s.Duration
            });
        db.AuditLogs.Add(Audit.Entry(user, "appointment.rescheduled", nameof(Appointment), appt.Id, $"start={appt.StartAt:O}; barber={barberId}", now));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(CurrentUser user, Guid id, AppointmentStatusInput input, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AppointmentManage))
            throw AppException.Forbidden("ليس لديك صلاحية لإدارة المواعيد");
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == user.OrganizationId, ct)
            ?? throw AppException.NotFound("الموعد غير موجود");
        await EnsureBranchAccessAsync(user, appt.BranchId, ct);

        if (appt.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
            throw AppException.Conflict("appointment_closed", "هذا الموعد مغلق");

        var allowed = input.Status switch
        {
            AppointmentStatus.Confirmed => appt.Status == AppointmentStatus.Pending,
            AppointmentStatus.InService => appt.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.Waiting,
            AppointmentStatus.Cancelled or AppointmentStatus.NoShow => true,
            _ => false
        };
        if (!allowed)
            throw AppException.Bad("invalid_transition", "لا يمكن نقل الموعد لهذه الحالة — يكتمل الموعد تلقائيًا عند تسجيل البيع");

        var now = DateTime.UtcNow;
        if (input.Status == AppointmentStatus.InService)
        {
            var barberId = input.BarberId ?? appt.BarberId
                ?? throw AppException.Bad("barber_required", "اختر الحلاق لبدء الخدمة");
            await EnsureBarberInBranchAsync(user.OrganizationId, barberId, appt.BranchId, ct);
            var busy = await db.Appointments.AnyAsync(a => a.Id != appt.Id && a.BarberId == barberId
                                                            && a.Status == AppointmentStatus.InService, ct);
            if (busy) throw AppException.Conflict("barber_busy", "الحلاق مشغول بعميل آخر الآن");
            appt.BarberId = barberId;
            if (appt.Status == AppointmentStatus.Waiting)
            {
                // a walk-in starts now: re-anchor its time window
                var duration = appt.EndAt - appt.StartAt;
                appt.StartAt = now;
                appt.EndAt = now + duration;
            }
        }
        if (input.Status == AppointmentStatus.Cancelled)
            appt.CancelReason = string.IsNullOrWhiteSpace(input.Reason) ? "ألغاه المحل" : input.Reason.Trim();

        appt.Status = input.Status;
        appt.UpdatedAt = now;
        db.AuditLogs.Add(Audit.Entry(user, "appointment." + input.Status.ToString().ToLowerInvariant(), nameof(Appointment), appt.Id, input.Reason, now));
        await db.SaveChangesAsync(ct);
    }

    public async Task<object> StaffSlotsAsync(CurrentUser user, Guid branchId, Guid? barberId, DateOnly date, List<Guid> serviceIds, Guid? excludeId, CancellationToken ct = default)
    {
        if (!Permissions.Can(user, Permissions.AppointmentView))
            throw AppException.Forbidden("ليس لديك صلاحية لعرض المواعيد");
        await EnsureBranchAccessAsync(user, branchId, ct);
        return await SlotsAsync(user.OrganizationId, branchId, barberId, date, serviceIds, excludeId, ct);
    }

    // ======================= Public (online booking) =======================

    public async Task<object> PublicInfoAsync(string slug, CancellationToken ct = default)
    {
        var org = await ResolvePublicOrgAsync(slug, ct);
        var settings = await db.OrganizationSettings.AsNoTracking().FirstAsync(s => s.OrganizationId == org.Id, ct);
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.OrganizationId == org.Id && b.Status == EntityStatus.Active)
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.City, b.Address, b.Phone, OpensAt = b.OpensAt, ClosesAt = b.ClosesAt })
            .ToListAsync(ct);
        var services = await db.Services.AsNoTracking()
            .Where(s => s.OrganizationId == org.Id && s.Status == EntityStatus.Active)
            .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Description, Price = s.BasePrice, s.DurationMinutes }).ToListAsync(ct);
        var barbers = await db.Barbers.AsNoTracking()
            .Where(b => b.OrganizationId == org.Id && b.Status == EntityStatus.Active)
            .OrderBy(b => b.FullName)
            .Select(b => new { b.Id, b.FullName, BranchIds = b.BranchAssignments.Select(a => a.BranchId).ToList() }).ToListAsync(ct);

        var tz = OrgTime.Resolve(org.TimeZone);
        return new
        {
            organization = new { org.Name, org.LogoUrl, org.PrimaryColor, org.SecondaryColor, org.CoverImageUrl },
            branches = branches.Select(b => new { b.Id, b.Name, b.City, b.Address, b.Phone, OpensAt = b.OpensAt.ToString("HH\\:mm"), ClosesAt = b.ClosesAt.ToString("HH\\:mm") }),
            services,
            barbers,
            today = OrgTime.Today(tz),
            maxDaysAhead = settings.BookingMaxDaysAhead
        };
    }

    public async Task<object> PublicSlotsAsync(string slug, Guid branchId, Guid? barberId, DateOnly date, List<Guid> serviceIds, CancellationToken ct = default)
    {
        var org = await ResolvePublicOrgAsync(slug, ct);
        return await SlotsAsync(org.Id, branchId, barberId, date, serviceIds, null, ct);
    }

    public async Task<object> PublicBookAsync(string slug, PublicBookingInput input, CancellationToken ct = default)
    {
        var org = await ResolvePublicOrgAsync(slug, ct);
        var (tz, settings) = await OrgContextAsync(org.Id, ct);

        var name = (input.Name ?? "").Trim();
        if (name.Length is < 2 or > 100) throw AppException.Bad("name_required", "اكتب اسمك");
        var phone = NormalizePhone(input.Phone) ?? throw AppException.Bad("invalid_phone", "رقم الجوال غير صحيح — مثال: 05XXXXXXXX");

        var today = OrgTime.Today(tz);
        if (input.Date < today || input.Date > today.AddDays(settings.BookingMaxDaysAhead))
            throw AppException.Bad("invalid_date", $"يمكن الحجز خلال {settings.BookingMaxDaysAhead} يومًا القادمة فقط");

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == input.BranchId && b.OrganizationId == org.Id && b.Status == EntityStatus.Active, ct)
            ?? throw AppException.Bad("invalid_branch", "الفرع غير موجود");
        var services = await LoadServicesAsync(org.Id, branch.Id, input.ServiceIds, ct);
        var duration = Math.Max(settings.BookingSlotMinutes, services.Sum(s => s.Duration));
        if (input.BarberId is not null) await EnsureBarberInBranchAsync(org.Id, input.BarberId.Value, branch.Id, ct);

        var nowUtc = DateTime.UtcNow;
        var activeForPhone = await db.Appointments.CountAsync(a => a.OrganizationId == org.Id && a.CustomerPhone == phone
            && a.Source == AppointmentSource.Online && a.StartAt > nowUtc && Blocking.Contains(a.Status), ct);
        if (activeForPhone >= MaxActiveOnlineBookingsPerPhone)
            throw AppException.Bad("too_many_bookings", "لديك حجوزات قادمة كثيرة — تواصل مع المحل");

        var startUtc = ParseLocal(input.Date, input.Time, tz);
        if (startUtc < nowUtc.AddMinutes(10)) throw AppException.Bad("past_time", "هذا الوقت لم يعد متاحًا");
        EnsureWithinHours(branch, startUtc, duration, tz);

        await using var tx = await db.BeginTransactionAsync(ct);
        var barberId = await AssignBarberAsync(org.Id, branch.Id, input.BarberId, startUtc, startUtc.AddMinutes(duration), null, ct);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.OrganizationId == org.Id && c.Phone == phone, ct);
        if (customer is null)
        {
            customer = new Customer { Id = Guid.NewGuid(), OrganizationId = org.Id, FullName = name, Phone = phone, Notes = "أُنشئ من الحجز الإلكتروني", CreatedAt = nowUtc, UpdatedAt = nowUtc };
            db.Customers.Add(customer);
        }

        var appt = new Appointment
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            BranchId = branch.Id,
            BarberId = barberId,
            CustomerId = customer.Id,
            CustomerName = name,
            CustomerPhone = phone,
            StartAt = startUtc,
            EndAt = startUtc.AddMinutes(duration),
            Status = AppointmentStatus.Confirmed,
            Source = AppointmentSource.Online,
            Notes = input.Notes?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 300)] : null,
            Reference = await NewReferenceAsync(org.Id, ct),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            Items = services.Select(s => new AppointmentItem
            {
                Id = Guid.NewGuid(), ServiceId = s.Id, ServiceName = s.Name, Price = s.Price, DurationMinutes = s.Duration
            }).ToList()
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var barberName = barberId is null ? null : await db.Barbers.Where(b => b.Id == barberId).Select(b => b.FullName).FirstAsync(ct);
        var local = OrgTime.ToLocal(startUtc, tz);
        return new
        {
            appt.Reference,
            date = DateOnly.FromDateTime(local),
            time = local.ToString("HH:mm"),
            branch = branch.Name,
            barber = barberName,
            services = services.Select(s => s.Name),
            total = services.Sum(s => s.Price),
            message = "تم تأكيد حجزك"
        };
    }

    public async Task PublicCancelAsync(string slug, string reference, string phone, CancellationToken ct = default)
    {
        var org = await ResolvePublicOrgAsync(slug, ct);
        var normalized = NormalizePhone(phone) ?? throw AppException.Bad("invalid_phone", "رقم الجوال غير صحيح");
        var refCode = (reference ?? "").Trim().ToUpperInvariant();
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.OrganizationId == org.Id && a.Reference == refCode && a.CustomerPhone == normalized, ct)
            ?? throw AppException.NotFound("لم نجد حجزًا بهذه البيانات");
        if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw AppException.Conflict("cannot_cancel", "لا يمكن إلغاء هذا الحجز");
        if (appt.StartAt < DateTime.UtcNow)
            throw AppException.Conflict("cannot_cancel", "موعد الحجز مضى");
        appt.Status = AppointmentStatus.Cancelled;
        appt.CancelReason = "ألغاه العميل";
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ======================= Slots =======================

    private async Task<object> SlotsAsync(Guid orgId, Guid branchId, Guid? barberId, DateOnly date, List<Guid> serviceIds, Guid? excludeId, CancellationToken ct)
    {
        var (tz, settings) = await OrgContextAsync(orgId, ct);
        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId && b.OrganizationId == orgId, ct)
            ?? throw AppException.Bad("invalid_branch", "الفرع غير موجود");
        var services = await LoadServicesAsync(orgId, branchId, serviceIds, ct);
        var duration = Math.Max(settings.BookingSlotMinutes, services.Sum(s => s.Duration));

        var barberIds = await db.Barbers.AsNoTracking()
            .Where(b => b.OrganizationId == orgId && b.Status == EntityStatus.Active && b.BranchAssignments.Any(a => a.BranchId == branchId))
            .Select(b => b.Id).ToListAsync(ct);
        if (barberId is not null)
        {
            if (!barberIds.Contains(barberId.Value)) throw AppException.Bad("barber_not_in_branch", "الحلاق غير مسند لهذا الفرع");
            barberIds = new List<Guid> { barberId.Value };
        }

        var (dayFrom, dayTo) = DayRangeUtc(date, tz);
        var busy = await db.Appointments.AsNoTracking()
            .Where(a => a.BranchId == branchId && a.BarberId != null && Blocking.Contains(a.Status)
                        && a.StartAt < dayTo && a.EndAt > dayFrom && a.Id != excludeId)
            .Select(a => new { BarberId = a.BarberId!.Value, a.StartAt, a.EndAt }).ToListAsync(ct);
        // Unassigned bookings consume capacity from the whole branch
        var unassigned = await db.Appointments.AsNoTracking()
            .Where(a => a.BranchId == branchId && a.BarberId == null && Blocking.Contains(a.Status)
                        && a.StartAt < dayTo && a.EndAt > dayFrom && a.Id != excludeId)
            .Select(a => new { a.StartAt, a.EndAt }).ToListAsync(ct);

        var step = Math.Max(5, settings.BookingSlotMinutes);
        var nowUtc = DateTime.UtcNow;
        var slots = new List<object>();
        var openLocal = date.ToDateTime(branch.OpensAt);
        var closeLocal = branch.ClosesAt <= branch.OpensAt ? date.AddDays(1).ToDateTime(branch.ClosesAt) : date.ToDateTime(branch.ClosesAt);
        for (var t = openLocal; t.AddMinutes(duration) <= closeLocal; t = t.AddMinutes(step))
        {
            var start = OrgTime.ToUtc(t, tz);
            var end = start.AddMinutes(duration);
            if (start < nowUtc.AddMinutes(10)) continue;
            var free = barberIds.Where(id => !busy.Any(x => x.BarberId == id && x.StartAt < end && x.EndAt > start)).ToList();
            var overlapUnassigned = unassigned.Count(x => x.StartAt < end && x.EndAt > start);
            if (free.Count - (barberId is null ? overlapUnassigned : 0) <= 0) continue;
            slots.Add(new { time = t.ToString("HH:mm"), startAt = start, freeBarbers = free.Count });
        }
        return new { date, durationMinutes = duration, slots };
    }

    // ======================= Helpers =======================

    private async Task<Guid?> AssignBarberAsync(Guid orgId, Guid branchId, Guid? barberId, DateTime start, DateTime end, Guid? excludeId, CancellationToken ct)
    {
        var overlapping = await db.Appointments
            .Where(a => a.BranchId == branchId && a.Id != excludeId && Blocking.Contains(a.Status) && a.StartAt < end && a.EndAt > start)
            .Select(a => a.BarberId).ToListAsync(ct);

        if (barberId is not null)
        {
            if (overlapping.Contains(barberId))
                throw AppException.Conflict("slot_taken", "الحلاق لديه موعد آخر في هذا الوقت");
            return barberId;
        }

        var candidates = await db.Barbers
            .Where(b => b.OrganizationId == orgId && b.Status == EntityStatus.Active && b.BranchAssignments.Any(a => a.BranchId == branchId))
            .OrderBy(b => b.FullName).Select(b => b.Id).ToListAsync(ct);
        var unassignedCount = overlapping.Count(x => x is null);
        var free = candidates.Where(id => !overlapping.Contains(id)).ToList();
        if (free.Count - unassignedCount <= 0)
            throw AppException.Conflict("slot_taken", "لا يوجد حلاق متاح في هذا الوقت — اختر وقتًا آخر");
        return free[0];
    }

    private async Task<(TimeZoneInfo tz, OrganizationSettings settings)> OrgContextAsync(Guid orgId, CancellationToken ct)
    {
        var tzId = await db.Organizations.AsNoTracking().Where(o => o.Id == orgId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct)
                       ?? new OrganizationSettings { OrganizationId = orgId };
        return (OrgTime.Resolve(tzId), settings);
    }

    private async Task<Organization> ResolvePublicOrgAsync(string slug, CancellationToken ct)
    {
        var normalized = (slug ?? "").Trim().ToLowerInvariant();
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Slug == normalized, ct)
            ?? throw AppException.NotFound("صفحة الحجز غير موجودة");
        var enabled = await db.OrganizationSettings.AsNoTracking().AnyAsync(s => s.OrganizationId == org.Id && s.OnlineBookingEnabled, ct);
        if (!enabled) throw AppException.NotFound("الحجز الإلكتروني غير متاح حاليًا لهذا المحل");
        return org;
    }

    private async Task EnsureBranchAccessAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == branchId && b.OrganizationId == user.OrganizationId, ct))
            throw AppException.NotFound("الفرع غير موجود");
        if (user.Role == UserRole.Barber)
        {
            var ok = await db.BarberBranchAssignments.AnyAsync(a => a.BranchId == branchId
                && db.Barbers.Any(b => b.Id == a.BarberId && b.LinkedProfileId == user.ProfileId), ct);
            if (!ok) throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
            return;
        }
        if (!Permissions.CanAccessBranch(user, branchId))
            throw AppException.Forbidden("لا تملك صلاحية على هذا الفرع");
    }

    private async Task EnsureBarberInBranchAsync(Guid orgId, Guid barberId, Guid branchId, CancellationToken ct)
    {
        var ok = await db.Barbers.AnyAsync(b => b.Id == barberId && b.OrganizationId == orgId && b.Status == EntityStatus.Active
                                               && b.BranchAssignments.Any(a => a.BranchId == branchId), ct);
        if (!ok) throw AppException.Bad("barber_not_in_branch", "الحلاق غير متاح في هذا الفرع");
    }

    private record SvcInfo(Guid Id, string Name, decimal Price, int Duration);

    private async Task<List<SvcInfo>> LoadServicesAsync(Guid orgId, Guid branchId, List<Guid>? serviceIds, CancellationToken ct)
    {
        var ids = (serviceIds ?? new List<Guid>()).Distinct().ToList();
        if (ids.Count == 0) throw AppException.Bad("services_required", "اختر خدمة واحدة على الأقل");
        var services = await db.Services.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && ids.Contains(s.Id) && s.Status == EntityStatus.Active).ToListAsync(ct);
        if (services.Count != ids.Count) throw AppException.Bad("invalid_service", "إحدى الخدمات غير متاحة");
        var prices = await db.BranchServicePrices.AsNoTracking()
            .Where(p => p.BranchId == branchId && ids.Contains(p.ServiceId)).ToDictionaryAsync(p => p.ServiceId, p => p.Price, ct);
        return services.Select(s => new SvcInfo(s.Id, s.Name, prices.TryGetValue(s.Id, out var p) ? p : s.BasePrice, Math.Max(5, s.DurationMinutes))).ToList();
    }

    private async Task<(Guid? Id, string Name, string? Phone)> ResolveCustomerAsync(Guid orgId, Guid? customerId, string? name, string? phone, CancellationToken ct)
    {
        if (customerId is not null)
        {
            var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId && x.OrganizationId == orgId, ct)
                ?? throw AppException.Bad("invalid_customer", "العميل غير موجود");
            return (c.Id, c.FullName, c.Phone);
        }
        var trimmed = (name ?? "").Trim();
        var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : NormalizePhone(phone) ?? throw AppException.Bad("invalid_phone", "رقم الجوال غير صحيح");
        if (normalizedPhone is not null)
        {
            var existing = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.Phone == normalizedPhone, ct);
            if (existing is not null) return (existing.Id, existing.FullName, existing.Phone);
        }
        if (trimmed.Length == 0) throw AppException.Bad("customer_required", "اكتب اسم العميل أو اختره من القائمة");
        if (normalizedPhone is null) return (null, trimmed, null);

        // A phone number makes the visitor a known customer (visit history + loyalty)
        var now = DateTime.UtcNow;
        var created = new Customer { Id = Guid.NewGuid(), OrganizationId = orgId, FullName = trimmed, Phone = normalizedPhone, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(created);
        return (created.Id, created.FullName, created.Phone);
    }

    private static DateTime ParseLocal(DateOnly date, string time, TimeZoneInfo tz)
    {
        if (!TimeOnly.TryParseExact(time.Trim(), "HH:mm", out var t))
            throw AppException.Bad("invalid_time", "صيغة الوقت غير صحيحة");
        return OrgTime.ToUtc(date.ToDateTime(t), tz);
    }

    private static void EnsureWithinHours(Branch branch, DateTime startUtc, int durationMinutes, TimeZoneInfo tz)
    {
        var local = OrgTime.ToLocal(startUtc, tz);
        var open = DateOnly.FromDateTime(local).ToDateTime(branch.OpensAt);
        var close = branch.ClosesAt <= branch.OpensAt
            ? DateOnly.FromDateTime(local).AddDays(1).ToDateTime(branch.ClosesAt)
            : DateOnly.FromDateTime(local).ToDateTime(branch.ClosesAt);
        if (local < open || local.AddMinutes(durationMinutes) > close)
            throw AppException.Bad("outside_hours", $"الفرع يعمل من {branch.OpensAt:HH\\:mm} إلى {branch.ClosesAt:HH\\:mm}");
    }

    private static (DateTime from, DateTime to) DayRangeUtc(DateOnly day, TimeZoneInfo tz)
        => (OrgTime.ToUtc(day.ToDateTime(TimeOnly.MinValue), tz), OrgTime.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), tz));

    private async Task<string> NewReferenceAsync(Guid orgId, CancellationToken ct)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[6];
            for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            var reference = new string(chars);
            if (!await db.Appointments.AnyAsync(a => a.OrganizationId == orgId && a.Reference == reference, ct)) return reference;
        }
        throw new InvalidOperationException("Could not allocate a booking reference");
    }

    /// <summary>Normalizes Saudi mobile numbers to 05XXXXXXXX; returns null when invalid.</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00966")) digits = digits[5..];
        else if (digits.StartsWith("966")) digits = digits[3..];
        if (digits.StartsWith("5") && digits.Length == 9) digits = "0" + digits;
        return SaudiMobile().IsMatch(digits) ? digits : null;
    }

    /// <summary>International form for wa.me links (9665XXXXXXXX).</summary>
    public static string? ToInternational(string? phone)
        => NormalizePhone(phone) is { } p ? "966" + p[1..] : null;

    [GeneratedRegex("^05\\d{8}$")]
    private static partial Regex SaudiMobile();

    private static AppointmentView Map(Appointment a, TimeZoneInfo tz, IReadOnlyDictionary<Guid, string> barberNames)
    {
        var start = OrgTime.ToLocal(a.StartAt, tz);
        var end = OrgTime.ToLocal(a.EndAt, tz);
        return new AppointmentView(
            a.Id, a.Reference, a.BranchId, a.BarberId,
            a.BarberId is not null && barberNames.TryGetValue(a.BarberId.Value, out var n) ? n : null,
            a.CustomerId, a.CustomerName, a.CustomerPhone, ToInternational(a.CustomerPhone),
            a.StartAt, a.EndAt, start.ToString("yyyy-MM-dd"), start.ToString("HH:mm"), end.ToString("HH:mm"),
            (int)(a.EndAt - a.StartAt).TotalMinutes,
            a.Status, StatusText(a.Status), a.Source, a.Source switch
            {
                AppointmentSource.Online => "حجز إلكتروني",
                AppointmentSource.WalkIn => "حضور مباشر",
                _ => "حجز من المحل"
            },
            a.Notes, a.SaleId, a.CancelReason,
            a.Items.Select(i => new AppointmentItemView(i.ServiceId, i.ServiceName, i.Price, i.DurationMinutes)).ToList(),
            a.Items.Sum(i => i.Price));
    }

    public static string StatusText(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "بانتظار التأكيد",
        AppointmentStatus.Confirmed => "مؤكد",
        AppointmentStatus.Waiting => "في الانتظار",
        AppointmentStatus.InService => "قيد الخدمة",
        AppointmentStatus.Completed => "مكتمل",
        AppointmentStatus.Cancelled => "ملغى",
        _ => "لم يحضر"
    };
}
