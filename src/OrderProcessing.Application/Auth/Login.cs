using FluentValidation;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;

namespace OrderProcessing.Application.Auth;

public sealed class Login
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly IValidator<LoginRequest> _validator;

    public Login(
        IIdentityService identityService,
        ITokenService tokenService,
        IValidator<LoginRequest> validator)
    {
        _identityService = identityService;
        _tokenService = tokenService;
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

        var token = _tokenService.CreateAccessToken(user);
        return new LoginResponse(
            token.Value,
            token.ExpiresAt,
            new UserResponse(user.Id, user.Email, user.DisplayName, user.Roles));
    }
}
