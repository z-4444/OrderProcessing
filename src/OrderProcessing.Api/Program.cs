using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OrderProcessing.Api.Authorization;
using OrderProcessing.Api.Health;
using OrderProcessing.Api.Middleware;
using OrderProcessing.Application;
using OrderProcessing.Application.Common;
using OrderProcessing.Infrastructure;
using OrderProcessing.Infrastructure.Identity;
using OrderProcessing.Infrastructure.Messaging;
using OrderProcessing.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "OrderProcessing.Api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console());

    builder.Services.AddOptions<PricingOptions>()
        .Bind(builder.Configuration.GetSection(PricingOptions.SectionName));
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiAuthenticationAndAuthorization(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();

    var healthChecks = builder.Services.AddHealthChecks()
        .AddDbContextCheck<OrderProcessingDbContext>("database", tags: ["ready"]);

    var messaging = builder.Configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>();
    if (messaging?.Enabled == true)
    {
        healthChecks.AddRabbitMQ(
            $"amqp://{messaging.UserName}:{messaging.Password}@{messaging.HostName}:{messaging.Port}",
            name: "rabbitmq",
            tags: ["ready"]);
    }

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Order Processing API",
            Version = "v1",
            Description = "JWT-protected API for customers, products, inventory, and order lifecycle."
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT from POST /api/auth/login."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, exception) =>
        {
            if (httpContext.Request.Path.StartsWithSegments("/health"))
            {
                return LogEventLevel.Debug;
            }

            return exception is not null ? LogEventLevel.Error : LogEventLevel.Information;
        };

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
    });
    app.UseHttpsRedirection();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<UserLoggingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();

    var healthOptions = new HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
    };

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
    }).AllowAnonymous();

    app.MapHealthChecks("/health", healthOptions).AllowAnonymous();

    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
        await seeder.SeedAsync();

        if (app.Environment.IsDevelopment())
        {
            var dataSeeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
            await dataSeeder.SeedAsync();
        }
    }

    app.Run();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
