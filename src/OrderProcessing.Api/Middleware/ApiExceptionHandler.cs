using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Api.Middleware;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IHostEnvironment environment, ILogger<ApiExceptionHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            InsufficientInventoryException => (StatusCodes.Status409Conflict, "Insufficient inventory"),
            InvalidOrderTransitionException => (StatusCodes.Status409Conflict, "Invalid order transition"),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception {StatusCode}", status);
        }

        var correlationId = httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
            Detail = status == StatusCodes.Status500InternalServerError && !_environment.IsDevelopment()
                ? "An unexpected error occurred."
                : exception.Message
        };
        problem.Extensions["correlationId"] = correlationId;

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray());

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Status = status,
                Title = title,
                Type = problem.Type
            };
            validationProblem.Extensions["correlationId"] = correlationId;

            httpContext.Response.StatusCode = status;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
