namespace Hallagi.UnitTests;

public class AppointmentTests : IDisposable
{
    private readonly TestDb _db;
    private readonly AppointmentService _svc;
    private readonly DateOnly _tomorrow;

    public AppointmentTests()
    {
        _db = new TestDb();
        _svc = new AppointmentService(_db.Db);
        _tomorrow = OrgTime.Today(OrgTime.Resolve("Asia/Riyadh")).AddDays(1);
    }

    public void Dispose() => _db.Dispose();

    private AppointmentInput Booking(string time, Guid? barber = null, string? phone = "0551234567")
        => new(_db.BranchA, barber ?? _db.BarberId, null, "فهد", phone, _tomorrow, time, new List<Guid> { _db.SaleServiceId }, null);

    private void EnableOnlineBooking()
    {
        _db.Db.Organizations.Single().Slug = "test-salon";
        _db.Db.OrganizationSettings.Single().OnlineBookingEnabled = true;
        _db.Db.SaveChanges();
    }

    [Fact]
    public async Task SameBarber_SameTime_IsRejected()
    {
        await _svc.CreateAsync(_db.AsCashier(), Booking("18:00"));
        var ex = await Should.ThrowAsync<AppException>(() => _svc.CreateAsync(_db.AsCashier(), Booking("18:15", phone: "0559999999")));
        ex.Code.ShouldBe("slot_taken");
    }

    [Fact]
    public async Task Booking_OutsideBranchHours_IsRejected()
    {
        var ex = await Should.ThrowAsync<AppException>(() => _svc.CreateAsync(_db.AsCashier(), Booking("23:45")));
        ex.Code.ShouldBe("outside_hours");
    }

    [Fact]
    public async Task Booking_WithPhone_CreatesOrReusesCustomer()
    {
        await _svc.CreateAsync(_db.AsCashier(), Booking("10:00", phone: "+966 55 123 4567"));
        var appt = _db.Db.Appointments.Single();
        appt.CustomerPhone.ShouldBe("0551234567");
        appt.CustomerId.ShouldNotBeNull();
        appt.EndAt.ShouldBe(appt.StartAt.AddMinutes(30));
    }

    [Fact]
    public async Task Slots_ExcludeBookedTimes()
    {
        await _svc.CreateAsync(_db.AsCashier(), Booking("12:00"));
        var result = await _svc.StaffSlotsAsync(_db.AsCashier(), _db.BranchA, _db.BarberId, _tomorrow, new() { _db.SaleServiceId }, null);
        var times = ((System.Collections.IEnumerable)Ref.Val(result, "slots")!).Cast<object>().Select(s => Ref.Str(s, "time")).ToList();

        times.ShouldContain("11:30");
        times.ShouldNotContain("11:45");   // would overlap 12:00–12:30
        times.ShouldNotContain("12:00");
        times.ShouldContain("12:30");
        times.Last().ShouldBe("22:30");    // last 30-minute slot before 23:00 closing
    }

    [Fact]
    public async Task WalkIn_FlowsFromQueue_ToService_ToSale()
    {
        var created = await _svc.CreateAsync(_db.AsCashier(),
            new AppointmentInput(_db.BranchA, null, null, "زائر", null, null, null, new() { _db.SaleServiceId }, null, WalkIn: true));
        var id = Ref.Id(created, "id");
        _db.Db.Appointments.Single().Status.ShouldBe(AppointmentStatus.Waiting);

        await _svc.SetStatusAsync(_db.AsCashier(), id, new AppointmentStatusInput(AppointmentStatus.InService, null, _db.BarberId));

        var sales = new SalesService(_db.Db, _db.Clock);
        await sales.CreateAsync(_db.AsCashier(), new CreateSaleRequest(_db.BranchA, _db.BarberId, null, PaymentMethod.Card,
            new() { new(_db.SaleServiceId, 1) }, "walk-1", AppointmentId: id));

        _db.Db.ChangeTracker.Clear();
        var appt = _db.Db.Appointments.Single();
        appt.Status.ShouldBe(AppointmentStatus.Completed);
        appt.SaleId.ShouldNotBeNull();

        var again = await Should.ThrowAsync<AppException>(() => sales.CreateAsync(_db.AsCashier(), new CreateSaleRequest(_db.BranchA, _db.BarberId, null,
            PaymentMethod.Card, new() { new(_db.SaleServiceId, 1) }, "walk-2", AppointmentId: id)));
        again.Code.ShouldBe("appointment_closed");
    }

    [Fact]
    public async Task Completed_IsOnlyReachedThroughASale()
    {
        var created = await _svc.CreateAsync(_db.AsCashier(), Booking("15:00"));
        var ex = await Should.ThrowAsync<AppException>(() =>
            _svc.SetStatusAsync(_db.AsCashier(), Ref.Id(created, "id"), new AppointmentStatusInput(AppointmentStatus.Completed, null, null)));
        ex.Code.ShouldBe("invalid_transition");
    }

    [Fact]
    public async Task Barber_CannotManageAppointments()
    {
        var ex = await Should.ThrowAsync<AppException>(() => _svc.CreateAsync(_db.AsBarber(), Booking("16:00")));
        ex.Status.ShouldBe(403);
    }

    [Fact]
    public async Task PublicBooking_IsClosed_UntilTheOwnerEnablesIt()
    {
        _db.Db.Organizations.Single().Slug = "test-salon";
        _db.Db.SaveChanges();
        var ex = await Should.ThrowAsync<AppException>(() => _svc.PublicInfoAsync("test-salon"));
        ex.Status.ShouldBe(404);
    }

    [Fact]
    public async Task PublicBooking_AssignsAFreeBarber_AndCanBeCancelledByTheCustomer()
    {
        EnableOnlineBooking();
        var result = await _svc.PublicBookAsync("test-salon", new PublicBookingInput(
            _db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, "13:00", "عبدالرحمن", "0501112233", null));

        var reference = Ref.Str(result, "reference");
        reference.Length.ShouldBe(6);
        var appt = _db.Db.Appointments.Single();
        appt.BarberId.ShouldBe(_db.BarberId);
        appt.Source.ShouldBe(AppointmentSource.Online);

        // the only barber is now busy at 13:00
        var taken = await Should.ThrowAsync<AppException>(() => _svc.PublicBookAsync("test-salon", new PublicBookingInput(
            _db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, "13:00", "آخر", "0507778899", null)));
        taken.Code.ShouldBe("slot_taken");

        var wrongPhone = await Should.ThrowAsync<AppException>(() => _svc.PublicCancelAsync("test-salon", reference, "0500000000"));
        wrongPhone.Status.ShouldBe(404);

        await _svc.PublicCancelAsync("test-salon", reference.ToLowerInvariant(), "966501112233");
        _db.Db.Appointments.Single().Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task PublicBooking_LimitsActiveBookingsPerPhone()
    {
        EnableOnlineBooking();
        foreach (var time in new[] { "10:00", "11:00", "12:00" })
            await _svc.PublicBookAsync("test-salon", new PublicBookingInput(_db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, time, "سامي", "0501112233", null));

        var ex = await Should.ThrowAsync<AppException>(() => _svc.PublicBookAsync("test-salon",
            new PublicBookingInput(_db.BranchA, null, new() { _db.SaleServiceId }, _tomorrow, "14:00", "سامي", "0501112233", null)));
        ex.Code.ShouldBe("too_many_bookings");
    }

    [Theory]
    [InlineData("0551234567", "0551234567")]
    [InlineData("551234567", "0551234567")]
    [InlineData("+966551234567", "0551234567")]
    [InlineData("00966 55 123 4567", "0551234567")]
    [InlineData("0112345678", null)]
    [InlineData("abc", null)]
    public void SaudiPhone_Normalization(string input, string? expected) => AppointmentService.NormalizePhone(input).ShouldBe(expected);
}
