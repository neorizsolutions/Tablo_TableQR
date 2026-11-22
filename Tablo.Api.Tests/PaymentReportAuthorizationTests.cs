using System.Net;
using System.Net.Http.Headers;

namespace Tablo.Api.Tests;

public class PaymentReportAuthorizationTests(ServiceDeskTestHost host) : IClassFixture<ServiceDeskTestHost>
{
    public static IEnumerable<object[]> Cases()
    {
        foreach(var endpoint in new[]{"", "export", "receipt"})
            foreach(var identity in new[]{"anonymous","Waiter","Kitchen","Admin","foreign-tenant","missing-tenant","inactive","forged-admin"})
                yield return [endpoint,identity];
    }
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AllPaymentEndpoints_RequireActiveAdminAndMatchingTenant(string endpoint,string identity)
    {
        using var client=host.CreateClient(); await host.SeedAsync();
        if(identity!="anonymous")
        {
            var role=identity is "Waiter" or "Kitchen"?identity:"Admin";
            Guid? tenant=identity=="missing-tenant"?null:host.Restaurant;
            var actor=identity=="inactive"?host.InactiveActor:identity=="forged-admin"?host.Actor:host.AdminActor;
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",host.Token(role,tenant,actor));
        }
        var route=identity=="foreign-tenant"?Guid.NewGuid():host.Restaurant;
        var suffix=endpoint=="receipt"?$"{Guid.NewGuid()}/receipt":endpoint;
        var response=await client.GetAsync($"/api/restaurants/{route}/payment-reports/{suffix}");
        var expected=identity=="anonymous"?HttpStatusCode.Unauthorized:identity!="Admin"?HttpStatusCode.Forbidden:
            endpoint=="receipt"?HttpStatusCode.NotFound:HttpStatusCode.OK;
        Assert.Equal(expected,response.StatusCode);
    }
}
