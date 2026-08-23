using FluentValidation;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;

namespace OrderProcessing.Application.Auth;

public sealed class Login
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IValidator<LoginRequest> _validator;

    public Login(
        IIdentityService identityService,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IValidator<LoginRequest> validator)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _validator = validator;
    }

    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        const string invalidMessage = "Invalid email or password.";
        var user = await _identityService.FindByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException(invalidMessage);
        }

        var passwordValid = await _identityService.CheckPasswordAsync(user.Id, request.Password, cancellationToken);
        if (!passwordValid)
        {
            throw new AuthenticationFailedException(invalidMessage);
        }

        var access = _tokenService.CreateAccessToken(user);
        var refresh = await _refreshTokenService.IssueAsync(user.Id, cancellationToken);
        return new LoginResponse(
            access.Value,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt,
            new UserResponse(user.Id, user.Email, user.DisplayName, user.Roles));
    }
}

public sealed class RefreshAccessToken
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RefreshTokenRequest> _validator;

    public RefreshAccessToken(
        IRefreshTokenService refreshTokenService,
        ITokenService tokenService,
        IValidator<RefreshTokenRequest> validator)
    {
        _refreshTokenService = refreshTokenService;
        _tokenService = tokenService;
        _validator = validator;
    }

    public async Task<LoginResponse> Handle(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var rotated = await _refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);
        if (rotated is null)
        {
            throw new AuthenticationFailedException("Invalid or expired refresh token.");
        }

        var (user, refresh) = rotated.Value;
        var access = _tokenService.CreateAccessToken(user);
        return new LoginResponse(
            access.Value,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt,
            new UserResponse(user.Id, user.Email, user.DisplayName, user.Roles));
    }
}

public sealed class Logout
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IValidator<RefreshTokenRequest> _validator;

    public Logout(IRefreshTokenService refreshTokenService, IValidator<RefreshTokenRequest> validator)
    {
        _refreshTokenService = refreshTokenService;
        _validator = validator;
    }

    public async Task Handle(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        await _refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
    }
}
