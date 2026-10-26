using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hallagi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ApiTests(HallagiApiFactory factory)
{
    private const string Password = "Hallagi@123";

    private async Task<(HttpClient client, JsonElement login)> SignInAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString());
        return (client, login);
    }

    [Fact]
    public async Task HealthEndpoints_ReportLiveAndReady()
    {
        var client = factory.CreateClient();
        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/sales");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WrongPassword_ReturnsArabicError()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "owner@hallagi.sa", password = "nope" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetProperty("message").GetString()!.ShouldContain("كلمة المرور");
    }

    [Fact]
    public async Task Login_Me_Refresh_Logout_Flow()
    {
        var (client, login) = await SignInAsync("owner@hallagi.sa");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("role").GetString().ShouldBe("Owner");
        me.GetProperty("permissions").GetProperty("appointment.manage").GetBoolean().ShouldBeTrue();

        var refresh = login.GetProperty("refreshToken").GetString();
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();
        rotated.ShouldNotBe(refresh);

        (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = rotated })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PosSale_WithVat_ReturnsTaxInvoiceWithZatcaQr()
    {
        var (client, login) = await SignInAsync("owner@hallagi.sa");
        var branchId = login.GetProperty("branches")[0].GetProperty("id").GetString();

        var pos = await client.GetFromJsonAsync<JsonElement>($"/api/pos/data?branchId={branchId}");
        pos.GetProperty("pricing").GetProperty("vatEnabled").GetBoolean().ShouldBeTrue();
        var service = pos.GetProperty("services")[0];
        var barber = pos.GetProperty("barbers")[0];

        var response = await client.PostAsJsonAsync("/api/sales", new
        {
            branchId,
            barberId = barber.GetProperty("id").GetString(),
            paymentMethod = "Card",
            idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { serviceId = service.GetProperty("id").GetString(), quantity = 1 } }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sale = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sale");
        sale.GetProperty("isTaxInvoice").GetBoolean().ShouldBeTrue();
        sale.GetProperty("vatAmount").GetDecimal().ShouldBeGreaterThan(0m);
        sale.GetProperty("invoice").GetProperty("zatcaQr").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Cashier_CannotOpenOwnerOnlyScreens()
    {
        var (client, _) = await SignInAsync("cashier@hallagi.sa");
        (await client.GetAsync("/api/coupons")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicBookingPage_IsAnonymous_AndOffersSlots()
    {
        var client = factory.CreateClient();
        var info = await client.GetFromJsonAsync<JsonElement>("/api/public/elite");
        info.GetProperty("organization").GetProperty("name").GetString().ShouldNotBeNullOrEmpty();

        var branchId = info.GetProperty("branches")[0].GetProperty("id").GetString();
        var serviceId = info.GetProperty("services")[0].GetProperty("id").GetString();
        var tomorrow = info.GetProperty("today").GetDateTime().AddDays(1).ToString("yyyy-MM-dd");
        var slots = await client.GetFromJsonAsync<JsonElement>($"/api/public/elite/slots?branchId={branchId}&date={tomorrow}&serviceIds={serviceId}");
        slots.GetProperty("slots").GetArrayLength().ShouldBeGreaterThan(0);

        (await client.GetAsync("/api/public/does-not-exist")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
