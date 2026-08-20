using FluentValidation;

namespace OrderProcessing.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(Guid Id, string Email, string? Name, IReadOnlyList<string> Roles);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}
