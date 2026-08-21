using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Infrastructure.Identity;
using System.Text;

namespace OrderProcessing.Api.Authorization;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddApiAuthenticationAndAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters.");
        }

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.CustomersRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales));
            options.AddPolicy(Policies.CustomersWrite, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales));
            options.AddPolicy(Policies.ProductsRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales, Roles.Warehouse));
            options.AddPolicy(Policies.ProductsManage, policy => policy.RequireRole(Roles.Admin, Roles.Manager));
            options.AddPolicy(Policies.OrdersRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales, Roles.Warehouse));
            options.AddPolicy(Policies.OrdersCreate, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales));
            options.AddPolicy(Policies.OrdersEdit, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales));
            options.AddPolicy(Policies.OrdersSubmit, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales));
            options.AddPolicy(Policies.OrdersConfirm, policy => policy.RequireRole(Roles.Admin, Roles.Manager));
            options.AddPolicy(Policies.OrdersProcess, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Warehouse));
            options.AddPolicy(Policies.OrdersShip, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Warehouse));
            options.AddPolicy(Policies.OrdersComplete, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Warehouse));
            options.AddPolicy(Policies.OrdersCancel, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Sales));
            options.AddPolicy(Policies.OrdersFail, policy => policy.RequireRole(Roles.Admin, Roles.Manager));
            options.AddPolicy(Policies.InventoryRead, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Warehouse));
            options.AddPolicy(Policies.InventoryAdjust, policy => policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Warehouse));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        return services;
    }
}

internal sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList() ?? [];
}
