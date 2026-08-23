using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}

internal sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly OrderProcessingDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenService(
        OrderProcessingDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(raw),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays))
        };

        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshTokenResult(raw, entity.ExpiresAt);
    }

    public async Task<(AuthenticatedUser User, RefreshTokenResult RefreshToken)?> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var existing = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (existing is null || !existing.IsActive)
        {
            return null;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        var user = await _userManager.FindByIdAsync(existing.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var authenticated = new AuthenticatedUser(user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive, roles.ToList());
        var replacement = await IssueAsync(user.Id, cancellationToken);
        return (authenticated, replacement);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(refreshToken);
        var existing = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
