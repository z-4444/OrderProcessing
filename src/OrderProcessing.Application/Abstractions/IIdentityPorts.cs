namespace OrderProcessing.Application.Abstractions;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IIdentityService
{
    Task<AuthenticatedUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(AuthenticatedUser user);
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }
}
