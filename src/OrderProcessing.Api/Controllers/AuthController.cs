using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Auth;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly Login _login;
    private readonly RefreshAccessToken _refreshAccessToken;
    private readonly Logout _logout;
    private readonly GetCurrentUser _getCurrentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        Login login,
        RefreshAccessToken refreshAccessToken,
        Logout logout,
        GetCurrentUser getCurrentUser,
        ILogger<AuthController> logger)
    {
        _login = login;
        _refreshAccessToken = refreshAccessToken;
        _logout = logout;
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

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _refreshAccessToken.Handle(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _logout.Handle(request, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserResponse> Me() => Ok(_getCurrentUser.Handle());
}
