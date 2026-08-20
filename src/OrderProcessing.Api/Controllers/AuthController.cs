using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Auth;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly Login _login;
    private readonly GetCurrentUser _getCurrentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(Login login, GetCurrentUser getCurrentUser, ILogger<AuthController> logger)
    {
        _login = login;
        _getCurrentUser = getCurrentUser;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _login.Handle(request, cancellationToken);
        _logger.LogInformation("User {Email} signed in", request.Email);
        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserResponse> Me() => Ok(_getCurrentUser.Handle());
}
