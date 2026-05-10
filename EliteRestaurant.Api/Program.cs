using System.Net;
using System.Threading.RateLimiting;
using EliteRestaurant.Api;
using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Api.Notifications;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Reservations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        DatabaseInitializer.Initialize(builder.Configuration);

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
                if (AppDbContext.TryGetPostgreSqlConnectionString(
                        out var cs,
                        builder.Configuration.GetConnectionString("DefaultConnection")))
                {
                    o.UseNpgsql(cs, n => n.EnableRetryOnFailure());
                }
                else if (AppDbContext.TryGetDatabaseUrlLastResort(out var databaseUrl))
                {
                    o.UseNpgsql(databaseUrl, n => n.EnableRetryOnFailure());
                }
                else
                {
                    Console.WriteLine(
                        "[EliteRestaurant] Warning: no PostgreSQL connection string was found during API startup. " +
                        "Continuing without configuring a database provider.");
                }
            },
            poolSize: 32);
    }

    builder.Services.AddControllers();
    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
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
                        && (context.HttpContext.Request.Path.StartsWithSegments("/hubs/order")
                            || context.HttpContext.Request.Path.StartsWithSegments("/hubs/reservation-floor")))
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
        options.AddPolicy("AdminRead", policy => policy.RequireRole("Admin", "Manager", "AdminWeb"));
        options.AddPolicy("AdminWrite", policy => policy.RequireRole("Admin", "Manager"));
        options.AddPolicy("OperationalWrite", policy => policy.RequireRole(
            "Admin", "Manager", "Chef", "Barman", "Bartender", "Sous Chef", "Cashier", "Server"));
        options.AddPolicy("ServerOnly", policy => policy.RequireRole("Server"));
        options.AddPolicy("CashierOnly", policy => policy.RequireRole("Cashier"));
        options.AddPolicy("KitchenOnly", policy => policy.RequireRole("Chef", "Barman", "Bartender", "Sous Chef"));
        options.AddPolicy("StaffAny", policy => policy.RequireAuthenticatedUser());
        // Reservation floor API + SignalR — Admin + Cashier only.
        options.AddPolicy("CashierOrAdmin", policy => policy.RequireRole("Admin", "Cashier"));
    });
    builder.Services.AddScoped<EliteRestaurant.Core.Reporting.AdminReportAggregationService>();
    builder.Services.Configure<ReservationSchedulingOptions>(builder.Configuration.GetSection("ReservationScheduling"));
    builder.Services.Configure<ReservationAutomationOptions>(builder.Configuration.GetSection("ReservationAutomation"));
    builder.Services.AddScoped<PlacementUnitClusterResolver>();
    builder.Services.AddScoped<ReservationSchedulingService>();
    builder.Services.AddScoped<FloorSnapshotBuilder>();
    builder.Services.AddSingleton<ReservationFloorRealtimePublisher>();
    builder.Services.AddSingleton<INotificationPublisher, LogNotificationPublisher>();
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddHostedService<ReservationNoShowProcessor>();
        builder.Services.AddHostedService<ReservationReminderProcessor>();
        builder.Services.AddHostedService<ReservationLifecycleProcessor>();
    }

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

    const string CorsPolicyAllowAll = "AllowAllOrigins";
    const string ProductionOrigin = "https://starfish-app-owtoz.ondigitalocean.app";

    var httpPort = GetHttpPort();

    builder.WebHost.ConfigureKestrel((_, options) =>
    {
        options.Listen(IPAddress.Any, httpPort);
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyAllowAll, policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    app.UseForwardedHeaders();

    app.UseResponseCompression();

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
                $"connect-src 'self' {ProductionOrigin};";
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

    app.UseCors(CorsPolicyAllowAll);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<EliteRestaurant.Api.Middleware.AdminWebReadOnlyApiMiddleware>();
    app.UseAuthorization();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapGet("/server", () => Results.Redirect("/server/index.html"));
    app.MapGet("/cashier", () => Results.Redirect("/cashier/index.html"));
    app.MapGet("/server/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var serverPortal = Path.Combine(env.WebRootPath, "server", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(serverPortal);
    });
    app.MapGet("/server/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var serverPortal = Path.Combine(env.WebRootPath, "server", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(serverPortal);
    });
    app.MapGet("/cashier/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var cashierPortal = Path.Combine(env.WebRootPath, "cashier", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(cashierPortal);
    });
    app.MapGet("/cashier/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var cashierPortal = Path.Combine(env.WebRootPath, "cashier", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(cashierPortal);
    });
    app.MapGet("/kitchen", () => Results.Redirect("/kitchen/index.html"));
    app.MapGet("/kitchen/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "kitchen", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/kitchen/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "kitchen", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));
    app.MapGet("/admin/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "admin", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/admin/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "admin", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(path);
    });
    app.MapControllers();
    app.MapHub<OrderHub>("/hubs/order");
    app.MapHub<ReservationFloorHub>("/hubs/reservation-floor");
    // Do not serve SPA index.html for /api/* — unknown API routes used to fall through and return HTML with 200,
    // which broke JSON clients (e.g. Create Order bundle on older deployments).
    app.MapFallback(async (HttpContext context) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new
            {
                message = $"No API endpoint matches '{context.Request.Path.Value}'.",
                path = context.Request.Path.Value
            });
            return;
        }

        // Staff portals live under /server/, /cashier/, /kitchen/. Do not serve the customer SPA for unknown paths there.
        if (context.Request.Path.StartsWithSegments("/cashier")
            || context.Request.Path.StartsWithSegments("/kitchen")
            || context.Request.Path.StartsWithSegments("/server")
            || context.Request.Path.StartsWithSegments("/admin"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var file = Path.Combine(env.WebRootPath, "index.html");
        if (!System.IO.File.Exists(file))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(file);
    });

    IntegrationTestSeed.Ensure(app);

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
