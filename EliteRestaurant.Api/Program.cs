using System.Net;
using System.Threading.RateLimiting;
using EliteRestaurant.Api;
using EliteRestaurant.Api.Hubs;
using EliteRestaurant.Api.Middleware;
using EliteRestaurant.Api.Options;
using EliteRestaurant.Api.Notifications;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Api.Services;
using EliteRestaurant.Api.Tenancy;
using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Tenancy;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Reservations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
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
    QuestPDF.Settings.License = LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
    }

    builder.Host.UseSerilog();

    if (builder.Environment.IsDevelopment()
        && !builder.Environment.IsEnvironment("Testing")
        && !EF.IsDesignTime)
        DatabaseInitializer.Initialize(builder.Configuration);

    var sentryDsn = builder.Configuration["Sentry:Dsn"];
    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = sentryDsn;
            options.Environment = builder.Environment.EnvironmentName;
            options.TracesSampleRate = builder.Environment.IsDevelopment() ? 0.1 : 0.02;
        });
    }

    builder.Services.AddMemoryCache();
    builder.Services.Configure<CurrencyPricingOptions>(builder.Configuration.GetSection("CurrencyPricing"));
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    builder.Services.Configure<AuthDevOptions>(builder.Configuration.GetSection("Auth"));
    builder.Services.Configure<EliteRestaurant.Api.Options.SetupOptions>(builder.Configuration.GetSection("Setup"));
    builder.Services.Configure<EliteRestaurant.Api.Options.TenancyOptions>(
        builder.Configuration.GetSection(EliteRestaurant.Api.Options.TenancyOptions.SectionName));
    builder.Services.Configure<LocalizationOptions>(builder.Configuration.GetSection("Localization"));
    builder.Services.Configure<EliteRestaurant.Api.Options.CorsOptions>(
        builder.Configuration.GetSection(EliteRestaurant.Api.Options.CorsOptions.SectionName));
    builder.Services.AddSingleton<LocalizationService>();
    builder.Services.AddSingleton<EliteRestaurant.Api.Services.CachedAppSettingsProvider>();
    builder.Services.AddScoped<EliteRestaurant.Api.Services.PublicMenuSettingsCache>();
    builder.Services.AddScoped<EliteRestaurant.Core.Utils.SharedOrderDraftService>();
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Cloud load balancers/proxies are dynamic. Limit exposure by only enabling this in deployed environments.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    if (builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<RestaurantTenantResolver>();
        builder.Services.AddDbContext<AppDbContext>((sp, o) =>
        {
            o.UseInMemoryDatabase("IntegrationTest");
        });
    }
    else
    {
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<RestaurantTenantResolver>();
        builder.Services.AddDbContext<AppDbContext>((sp, o) =>
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
        });
    }

    builder.Services.AddControllers();
    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    builder.Services.AddScoped<SiteSetupService>();
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
            "Admin", "Manager", "Chef", "Barman", "Bartender", "Sous Chef", "Cashier", "Server", "Front desk"));
        options.AddPolicy("ServerOnly", policy => policy.RequireRole("Server"));
        options.AddPolicy("CashierOnly", policy => policy.RequireRole("Cashier"));
        options.AddPolicy("KitchenOnly", policy => policy.RequireRole("Chef", "Sous Chef"));
        options.AddPolicy("BarOnly", policy => policy.RequireRole("Barman", "Bartender"));
        options.AddPolicy("StaffAny", policy => policy.RequireAuthenticatedUser());
        // Reservation floor API + SignalR — Admin, Manager, Cashier, and front desk.
        options.AddPolicy("CashierOrAdmin", policy => policy.RequireRole("Admin", "Manager", "Cashier", "Front desk"));
        // Reception / front desk (reservations, delivery & pickup tracking).
        options.AddPolicy("ReceptionDesk", policy => policy.RequireRole("Admin", "Manager", "Cashier", "Front desk"));
        options.AddPolicy("CashierDesk", policy => policy.RequireRole("Admin", "Manager", "Cashier"));
        options.AddPolicy("CancelOrder", policy => policy.RequireRole("Admin", "Manager", "Cashier", "Server"));
    });
    builder.Services.AddScoped<EliteRestaurant.Core.Reporting.AdminReportAggregationService>();
    builder.Services.Configure<ReservationSchedulingOptions>(builder.Configuration.GetSection("ReservationScheduling"));
    builder.Services.Configure<ReservationAutomationOptions>(builder.Configuration.GetSection("ReservationAutomation"));
    builder.Services.AddScoped<PlacementUnitClusterResolver>();
    builder.Services.AddScoped<ReservationSchedulingService>();
    builder.Services.AddScoped<FloorSnapshotBuilder>();
    builder.Services.AddScoped<EliteRestaurant.Core.Clients.ClientAccountService>();
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
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                GetPartitionKey(context),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
        options.AddPolicy("PublicMenuRead", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                GetPartitionKey(context),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
        options.AddPolicy("PublicMenuDraft", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                GetPartitionKey(context),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
        options.AddPolicy("Setup", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                GetPartitionKey(context),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
        options.AddPolicy("AuthLogin", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    static string GetPartitionKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    static int GetHttpPort()
    {
        var raw = Environment.GetEnvironmentVariable("PORT");
        if (int.TryParse(raw, out var port) && port > 0)
            return port;

        return 8080;
    }

    const string CorsPolicyConfigured = "ConfiguredOrigins";
    const string ProductionOrigin = "https://starfish-app-owtoz.ondigitalocean.app";

    var httpPort = GetHttpPort();

    builder.WebHost.ConfigureKestrel((_, options) =>
    {
        options.Listen(IPAddress.Any, httpPort);
    });

    var configuredCorsOrigins = builder.Configuration
        .GetSection(EliteRestaurant.Api.Options.CorsOptions.SectionName)
        .Get<string[]>() ?? [];
    if (configuredCorsOrigins.Length == 0)
    {
        configuredCorsOrigins =
        [
            ProductionOrigin,
            "http://localhost:8080",
            "http://127.0.0.1:8080",
            "http://localhost:5173",
            "http://127.0.0.1:5173"
        ];
    }

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyConfigured, policy =>
        {
            policy.WithOrigins(configuredCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionHandler>();

    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

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
                "style-src 'self'; " +
                "img-src 'self' blob: data:; " +
                "frame-src 'self' blob:; " +
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

    app.UseCors(CorsPolicyConfigured);
    app.UseRateLimiter();
    if (!app.Environment.IsEnvironment("Testing"))
        app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthentication();
    if (!app.Environment.IsEnvironment("Testing"))
        app.UseMiddleware<TenantJwtAlignmentMiddleware>();
    app.UseMiddleware<EliteRestaurant.Api.Middleware.AdminWebReadOnlyApiMiddleware>();
    app.UseAuthorization();
    static void ApplyHtmlNoStore(HttpResponse response)
    {
        response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Expires"] = "0";
    }
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                ApplyHtmlNoStore(context.Context.Response);
            }
        }
    });
    app.MapGet("/server", () => Results.Redirect("/server/index.html"));
    app.MapGet("/cashier", () => Results.Redirect("/cashier/index.html"));
    app.MapGet("/reception", () => Results.Redirect("/reception/index.html"));
    app.MapGet("/front-desk", () => Results.Redirect("/reception/index.html"));
    app.MapGet("/server/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var serverPortal = Path.Combine(env.WebRootPath, "server", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(serverPortal);
    });
    app.MapGet("/server/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var serverPortal = Path.Combine(env.WebRootPath, "server", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(serverPortal);
    });
    app.MapGet("/cashier/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var cashierPortal = Path.Combine(env.WebRootPath, "cashier", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(cashierPortal);
    });
    app.MapGet("/cashier/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var cashierPortal = Path.Combine(env.WebRootPath, "cashier", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(cashierPortal);
    });
    app.MapGet("/reception/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var receptionPortal = Path.Combine(env.WebRootPath, "reception", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(receptionPortal);
    });
    app.MapGet("/reception/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var receptionPortal = Path.Combine(env.WebRootPath, "reception", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(receptionPortal);
    });
    app.MapGet("/kitchen", () => Results.Redirect("/kitchen/index.html"));
    app.MapGet("/kitchen/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "kitchen", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/kitchen/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "kitchen", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/bar", () => Results.Redirect("/bar/index.html"));
    app.MapGet("/bar/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "bar", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/bar/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "bar", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));
    app.MapGet("/admin/", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "admin", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(path);
    });
    app.MapGet("/admin/index.html", async (IWebHostEnvironment env, HttpContext context) =>
    {
        var path = Path.Combine(env.WebRootPath, "admin", "index.html");
        context.Response.ContentType = "text/html; charset=utf-8";
        ApplyHtmlNoStore(context.Response);
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

        // Staff portals live under /server/, /cashier/, /reception/, /kitchen/. Do not serve the customer SPA for unknown paths there.
        if (context.Request.Path.StartsWithSegments("/cashier")
            || context.Request.Path.StartsWithSegments("/reception")
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
        ApplyHtmlNoStore(context.Response);
        await context.Response.SendFileAsync(file);
    });

    IntegrationTestSeed.Ensure(app);

    if (app.Environment.IsDevelopment())
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var demoSeed = DemoClientHistorySeed.Ensure(db);
            if (demoSeed == DemoClientHistorySeed.EnsureResult.Seeded)
                Log.Information("Demo client history seeded (15 regular clients with order history).");
            else if (demoSeed == DemoClientHistorySeed.EnsureResult.RepairedTenantScope)
                Log.Information("Demo client rows repaired for tenant scope.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Demo client seed skipped.");
        }
    }

    app.Run();
}
catch (HostAbortedException)
{
    // dotnet ef and other design-time tools build the host then abort it — not a runtime failure.
    throw;
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
