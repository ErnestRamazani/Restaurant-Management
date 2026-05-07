using System.Net;
using System.Threading.RateLimiting;
using EliteRestaurant.Api;
using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

// Do not set Npgsql.EnableLegacyTimestampBehavior — timestamps rely on UTC conversion in AppDbContext and Npgsql defaults.

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/elite-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    if (!builder.Environment.IsEnvironment("Testing"))
        DatabaseInitializer.Initialize();

    builder.Services.Configure<CurrencyPricingOptions>(builder.Configuration.GetSection("CurrencyPricing"));
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Cloud load balancers/proxies are dynamic. Limit exposure by only enabling this in deployed environments.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    if (builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase("IntegrationTest"));
    }
    else
    {
        builder.Services.AddDbContextPool<AppDbContext>(
            o =>
            {
                if (!AppDbContext.TryGetPostgreSqlConnectionString(
                        out var cs,
                        builder.Configuration.GetConnectionString("DefaultConnection")))
                {
                    if (!AppDbContext.TryGetDatabaseUrlLastResort(out cs))
                    {
                        throw new InvalidOperationException(
                            "PostgreSQL connection string is required for the API. Set DATABASE_URL, ELITE_POSTGRES_CONNECTION, " +
                            "or ConnectionStrings:DefaultConnection.");
                    }
                }

                o.UseNpgsql(cs, n => n.EnableRetryOnFailure(5));
            },
            poolSize: 32);
    }

    builder.Services.AddControllers();
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    builder.Services.AddScoped<TabletAuthService>();
    builder.Services.AddSingleton<JwtTokenService>();
    var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    var jwtValidation = new JwtTokenService(Options.Create(jwtOptions)).BuildValidationParameters();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = jwtValidation;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrWhiteSpace(accessToken)
                        && context.HttpContext.Request.Path.StartsWithSegments("/hubs/order"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "Manager"));
        options.AddPolicy("ServerOnly", policy => policy.RequireRole("Server"));
        options.AddPolicy("CashierOnly", policy => policy.RequireRole("Cashier"));
        options.AddPolicy("KitchenOnly", policy => policy.RequireRole("Chef", "Barman", "Bartender", "Sous Chef"));
        options.AddPolicy("StaffAny", policy => policy.RequireAuthenticatedUser());
    });
    builder.Services.AddSignalR();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("PublicMenuRead", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        options.AddPolicy("PublicMenuDraft", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    static string GetPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static int GetHttpPort()
    {
        var raw = Environment.GetEnvironmentVariable("PORT");
        if (int.TryParse(raw, out var port) && port > 0)
            return port;

        return 8080;
    }

    const string CorsPolicyRestrictToConfiguredOrigins = "RestrictToConfiguredOrigins";
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? Array.Empty<string>();

    var httpPort = GetHttpPort();

    builder.WebHost.ConfigureKestrel((_, options) =>
    {
        options.Listen(IPAddress.Any, httpPort);
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyRestrictToConfiguredOrigins, policy =>
        {
            if (corsOrigins.Length > 0)
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                // Same-origin browser hosting does not require CORS. Empty origin list means no cross-origin access.
                policy.SetIsOriginAllowed(_ => false)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
    });

    var app = builder.Build();

    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.00}ms";
    });

    app.Use(async (context, next) =>
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N")[..8];
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next();
        }
    });

    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' blob: data:; " +
                "connect-src 'self';";
        }

        await next();
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "EliteRestaurant API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseCors(CorsPolicyRestrictToConfiguredOrigins);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapControllers();
    app.MapHub<OrderHub>("/hubs/order");
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API host terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}
