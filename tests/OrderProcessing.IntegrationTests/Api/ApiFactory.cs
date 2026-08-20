using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Common;
using OrderProcessing.Infrastructure.Identity;

namespace OrderProcessing.IntegrationTests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:OrderProcessing", _connectionString);
        builder.UseSetting("Jwt:Issuer", "OrderProcessingTests");
        builder.UseSetting("Jwt:Audience", "OrderProcessingTests");
        builder.UseSetting("Jwt:Key", "TEST_ONLY_jwt_signing_key_32chars!!");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        builder.UseSetting("SeedAdmin:Email", "admin@localhost");
        builder.UseSetting("SeedAdmin:Password", "Admin_Pass_123!");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
        builder.UseSetting("Pricing:Currency", "USD");
        builder.UseSetting("Pricing:TaxRate", "0.08");
        builder.ConfigureTestServices(services => { });
    }

    public async Task EnsureWarehouseUserAsync()
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string email = "warehouse@localhost";
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Warehouse",
            IsActive = true
        };

        var created = await users.CreateAsync(user, "Warehouse_Pass_123!");
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        await users.AddToRoleAsync(user, Roles.Warehouse);
    }
}
