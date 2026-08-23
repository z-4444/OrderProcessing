using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Application.Abstractions;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record RefreshTokenResult(string Token, DateTimeOffset ExpiresAt);

public interface IIdentityService
{
    Task<AuthenticatedUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(AuthenticatedUser user);
}

public interface IRefreshTokenService
{
    Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<(AuthenticatedUser User, RefreshTokenResult RefreshToken)?> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }
}
