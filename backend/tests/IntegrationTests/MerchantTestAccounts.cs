using System.Net;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed record MerchantTestAccount(Guid Id, Guid ProfileId, string Name, string Email, string Password, AccountType Type);

public static class MerchantTestAccounts
{
    public static async Task<MerchantTestAccount> CreateAsync(IdentityApiFactory factory, AccountType type, bool confirmed = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HawalekDbContext>();
        await using var transaction = await context.BeginAsync();
        var name = "p04" + Guid.NewGuid().ToString("N");
        var password = "Test-only-" + Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(type, name, name + "@example.invalid"); user.EmailConfirmed = confirmed;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded);
        var profileId = Guid.Empty;
        if (type == AccountType.Merchant)
        {
            var profile = MerchantAccount.CreatePending(user.Id); context.Add(profile);
            await context.SaveChangesAsync(); profileId = profile.Id;
        }
        await transaction.CommitAsync();
        return new(user.Id, profileId, name, user.Email!, password, type);
    }

    public static async Task LoginAsync(HttpClient client, MerchantTestAccount account)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = account.Type.ToString(), identifier = account.Email, password = account.Password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public static async Task<string> VersionAsync(SqlDatabase database, Guid profileId)
    {
        await using var context = database.CreateContext();
        return Convert.ToBase64String((await context.Set<MerchantAccount>().AsNoTracking().SingleAsync(item => item.Id == profileId)).RowVersion);
    }
}
