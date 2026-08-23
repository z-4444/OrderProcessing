using System.Security.Claims;
using Serilog.Context;

namespace OrderProcessing.Api.Middleware;

/// <summary>
/// Pushes authenticated user properties into Serilog for the remainder of the request.
/// </summary>
public sealed class UserLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public UserLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("UserEmail", email))
        {
            await _next(context);
        }
    }
}
