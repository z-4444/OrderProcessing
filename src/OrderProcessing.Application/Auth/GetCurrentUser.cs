using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;

namespace OrderProcessing.Application.Auth;

public sealed class GetCurrentUser
{
    private readonly ICurrentUser _currentUser;

    public GetCurrentUser(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public UserResponse Handle()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new AuthenticationFailedException("The current user is not authenticated.");
        }

        return new UserResponse(
            _currentUser.UserId.Value,
            _currentUser.Email ?? string.Empty,
            _currentUser.Email,
            _currentUser.Roles);
    }
}
