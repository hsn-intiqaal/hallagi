namespace Hallagi.UnitTests;

public class RefreshTokenTests : IDisposable
{
    private readonly TestDb _db;
    private readonly AuthService _auth;

    public RefreshTokenTests()
    {
        _db = new TestDb();
        var owner = _db.Db.Profiles.Single(p => p.Id == _db.OwnerProfile);
        owner.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Secret#123");
        _db.Db.SaveChanges();
        _auth = TestServices.Auth(_db.Db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Refresh_Rotates_AndReuseRevokesTheWholeFamily()
    {
        var email = _db.Db.Profiles.Single(p => p.Id == _db.OwnerProfile).Email;
        var (ok, _, login) = await _auth.LoginAsync(email, "Secret#123", default);
        ok.ShouldBeTrue();
        var first = Ref.Str(login, "refreshToken");

        var (ok2, _, refreshed) = await _auth.RefreshAsync(first, default);
        ok2.ShouldBeTrue();
        var second = Ref.Str(refreshed, "refreshToken");
        second.ShouldNotBe(first);
        Ref.Str(refreshed, "token").ShouldNotBeNullOrEmpty();

        // replaying the rotated token = theft signal → every session of the profile is revoked
        var (replayOk, _, _) = await _auth.RefreshAsync(first, default);
        replayOk.ShouldBeFalse();
        var (secondOk, _, _) = await _auth.RefreshAsync(second, default);
        secondOk.ShouldBeFalse();
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var email = _db.Db.Profiles.Single(p => p.Id == _db.OwnerProfile).Email;
        var (_, _, login) = await _auth.LoginAsync(email, "Secret#123", default);
        var token = Ref.Str(login, "refreshToken");
        await _auth.LogoutAsync(token, default);
        (await _auth.RefreshAsync(token, default)).ok.ShouldBeFalse();
    }
}
