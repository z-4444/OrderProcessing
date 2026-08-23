using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Common;
using OrderProcessing.Infrastructure.Identity;

namespace OrderProcessing.IntegrationTests.Api;

public sealed record MessagingTestSettings(
    bool Enabled,
    string HostName,
    int Port,
    int PublisherIntervalSeconds = 1);

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly MessagingTestSettings? _messaging;

    public ApiFactory(string connectionString, MessagingTestSettings? messaging = null)
    {
        _connectionString = connectionString;
        _messaging = messaging;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:OrderProcessing", _connectionString);
        builder.UseSetting("Jwt:Issuer", "OrderProcessingTests");
        builder.UseSetting("Jwt:Audience", "OrderProcessingTests");
        builder.UseSetting("Jwt:Key", "TEST_ONLY_jwt_signing_key_32chars!!");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "7");
        builder.UseSetting("SeedAdmin:Email", "admin@localhost");
        builder.UseSetting("SeedAdmin:Password", "Admin_Pass_123!");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
        builder.UseSetting("Pricing:Currency", "USD");
        builder.UseSetting("Pricing:TaxRate", "0.08");

        if (_messaging?.Enabled == true)
        {
            builder.UseSetting("Messaging:Enabled", "true");
            builder.UseSetting("Messaging:HostName", _messaging.HostName);
            builder.UseSetting("Messaging:Port", _messaging.Port.ToString());
            builder.UseSetting("Messaging:UserName", "guest");
            builder.UseSetting("Messaging:Password", "guest");
            builder.UseSetting("Messaging:PublisherIntervalSeconds", _messaging.PublisherIntervalSeconds.ToString());
            builder.UseSetting("Messaging:PublisherBatchSize", "10");
            builder.UseSetting("Messaging:MaxPublishAttempts", "5");
        }
        else
        {
            builder.UseSetting("Messaging:Enabled", "false");
        }

        builder.ConfigureTestServices(services => { });
    }

    public Task EnsureWarehouseUserAsync() =>
        EnsureUserAsync(
            Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            "warehouse@localhost",
            "Warehouse_Pass_123!",
            "Warehouse",
            Roles.Warehouse);

    public Task EnsureSalesUserAsync() =>
        EnsureUserAsync(
            Guid.Parse("cccccccc-dddd-eeee-ffff-000000000001"),
            "sales@localhost",
            "Sales_Pass_123!",
            "Sales",
            Roles.Sales);

    private async Task EnsureUserAsync(
        Guid id,
        string email,
        string password,
        string displayName,
        string role)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            IsActive = true
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        await users.AddToRoleAsync(user, role);
    }
}
