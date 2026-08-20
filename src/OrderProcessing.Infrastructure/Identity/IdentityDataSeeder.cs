using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrderProcessing.Application.Common;

namespace OrderProcessing.Infrastructure.Identity;

public sealed class IdentityDataSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SeedAdminOptions _options;

    public IdentityDataSeeder(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<SeedAdminOptions> options)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to seed role '{roleName}': {Format(result)}");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(_options.Password) || string.IsNullOrWhiteSpace(_options.Email))
        {
            return;
        }

        var existing = await _userManager.FindByEmailAsync(_options.Email);
        if (existing is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            UserName = _options.Email,
            Email = _options.Email,
            EmailConfirmed = true,
            DisplayName = _options.DisplayName,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(admin, _options.Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to seed admin user: {Format(createResult)}");
        }

        var roleResult = await _userManager.AddToRoleAsync(admin, Roles.Admin);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to assign admin role: {Format(roleResult)}");
        }
    }

    private static string Format(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
