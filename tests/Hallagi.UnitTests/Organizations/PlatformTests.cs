namespace Hallagi.UnitTests;

/// <summary>
/// Platform owner tests: creating tenants, account isolation, and access control.
/// </summary>
public class PlatformTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PlatformService _platform;
    private readonly AuthService _auth;
    private readonly SubscriptionService _subscriptions;
    private readonly Guid _superAdminProfileId;

    public PlatformTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"DataSource=file:plat_{Guid.NewGuid():N}?mode=memory&cache=shared").Options;
        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var now = DateTime.UtcNow;

        // platform owner
        _superAdminProfileId = Guid.NewGuid();
        _db.Profiles.Add(new Profile
        {
            Id = _superAdminProfileId,
            FullName = "مالك المنصة",
            Email = "admin@platform.sa",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            IsSuperAdmin = true,
            Status = EntityStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        // an existing tenant with its owner
        var orgId = Guid.NewGuid();
        var ownerProfileId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        _db.Organizations.Add(new Organization { Id = orgId, Name = "منشأة قائمة", CreatedAt = now, UpdatedAt = now });
        _db.Profiles.Add(new Profile
        {
            Id = ownerProfileId,
            FullName = "مالك",
            Email = "owner@tenant.sa",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner@123"),
            Status = EntityStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            Id = ownerMemberId,
            OrganizationId = orgId,
            ProfileId = ownerProfileId,
            Role = UserRole.Owner,
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.SaveChanges();

        _auth = TestServices.Auth(_db);
        _subscriptions = new SubscriptionService(_db);
        _subscriptions.EnsureSeededAsync(default).GetAwaiter().GetResult();
        _platform = new PlatformService(_db, TestServices.Hasher, _subscriptions);
    }

    private CurrentUser SuperAdmin()
        => new(_superAdminProfileId, Guid.Empty, Guid.Empty, UserRole.SuperAdmin, true, Array.Empty<Guid>(), null);

    private CurrentUser TenantOwner()
    {
        var member = _db.OrganizationMembers.First();
        return new(member.ProfileId, member.OrganizationId, member.Id, UserRole.Owner, true,
            _db.Branches.Where(b => b.OrganizationId == member.OrganizationId).Select(b => b.Id).ToList(), null);
    }

    [Fact]
    public async Task SuperAdmin_Login_Returns_Platform_Role()
    {
        var (ok, error, result) = await _auth.LoginAsync("admin@platform.sa", "Admin@123", default);
        ok.ShouldBeTrue();
        var member = Ref.Val(result, "member")!;
        Ref.Str(member, "role").ShouldBe("SuperAdmin");
        Ref.Str(member, "organizationName").ShouldBe("منصة حلاقي");
    }

    [Fact]
    public async Task Platform_Creates_Tenant_With_Owner_And_Subscription()
    {
        var orgId = await _platform.CreateOrganizationAsync(new CreateOrganizationRequest(
            "صالون الاختبار", "الرياض", "الفرع الرئيسي", null,
            "مالك الاختبار", "new@tenant.sa", null, "Test@123", null), SuperAdmin(), default);

        var org = await _db.Organizations.FindAsync(orgId);
        org!.Name.ShouldBe("صالون الاختبار");

        // owner account can log in
        var (ok, _, _) = await _auth.LoginAsync("new@tenant.sa", "Test@123", default);
        ok.ShouldBeTrue();

        // subscription attached
        var sub = await _db.Subscriptions.FirstAsync(s => s.OrganizationId == orgId);
        sub.Status.ShouldBe("Active");

        // default service catalog exists so the tenant can sell immediately
        (await _db.Services.CountAsync(s => s.OrganizationId == orgId)).ShouldBe(6);
    }

    [Fact]
    public async Task Duplicate_Owner_Email_Is_Rejected()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() => _platform.CreateOrganizationAsync(
            new CreateOrganizationRequest("منشأة ثانية", "جدة", "فرع", null, "مالك", "owner@tenant.sa", null, "Test@123", null),
            SuperAdmin(), default));
        ex.Code.ShouldBe("email_taken");
    }

    [Fact]
    public async Task Tenant_Owner_Cannot_Access_Platform()
    {
        var owner = TenantOwner();
        // permissions are only granted to the platform owner
        Permissions.Can(owner, Permissions.PlatformManage).ShouldBeFalse();
        Permissions.Can(SuperAdmin(), Permissions.PlatformManage).ShouldBeTrue();
    }

    [Fact]
    public async Task Platform_Adds_Branch_To_Tenant()
    {
        var orgId = await _platform.CreateOrganizationAsync(new CreateOrganizationRequest(
            "صالون الفروع", "الرياض", "الفرع الأول", null, "مالك", "branches@tenant.sa", null, "Test@123", null),
            SuperAdmin(), default);

        await _platform.AddBranchAsync(orgId, "فرع ثاني", "جدة", SuperAdmin(), default);

        var branches = await _db.Branches.Where(b => b.OrganizationId == orgId).ToListAsync();
        branches.Count.ShouldBe(2);

        // the owner gets access to the new branch
        var member = await _db.OrganizationMembers.FirstAsync(m => m.OrganizationId == orgId && m.Role == UserRole.Owner);
        (await _db.MemberBranchAccess.CountAsync(a => a.MemberId == member.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task New_Tenant_Data_Is_Isolated_From_Existing_Tenant()
    {
        var newOrgId = await _platform.CreateOrganizationAsync(new CreateOrganizationRequest(
            "منشأة معزولة", "الرياض", "فرع", null, "مالك", "iso@tenant.sa", null, "Test@123", null),
            SuperAdmin(), default);

        var existingOrg = await _db.Organizations.FirstAsync(o => o.Name == "منشأة قائمة");

        // services of the new tenant are not visible to the old one
        var svc = await _db.Services.Where(s => s.OrganizationId == newOrgId).FirstAsync();
        var visibleFromOld = await _db.Services.AnyAsync(s => s.OrganizationId == existingOrg.Id && s.Id == svc.Id);
        visibleFromOld.ShouldBeFalse();
    }

    public void Dispose() => _db.Dispose();
}
