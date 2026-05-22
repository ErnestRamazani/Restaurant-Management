using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Tenancy;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

public sealed class SiteSetupService(AppDbContext db)
{
    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var count = await db.Restaurants.IgnoreQueryFilters().CountAsync(cancellationToken);
        return new SetupStatus(
            SetupRequired: count == 0,
            RestaurantCount: count,
            Message: count == 0
                ? "No restaurant site exists yet. Run first-site setup."
                : "At least one restaurant site is configured.");
    }

    public async Task<SiteSetupResult> CreateFirstSiteAsync(
        SiteSetupCommand request,
        CancellationToken cancellationToken = default)
    {
        if (await db.Restaurants.IgnoreQueryFilters().AnyAsync(cancellationToken))
            return SiteSetupResult.Fail(["Setup has already been completed. Use new-site setup to add another restaurant."]);

        return await CreateSiteCoreAsync(request, isFirstSite: true, cancellationToken);
    }

    public async Task<SiteSetupResult> CreateNewSiteAsync(
        SiteSetupCommand request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Restaurants.IgnoreQueryFilters().AnyAsync(cancellationToken))
            return SiteSetupResult.Fail(["Run first-site setup before adding another restaurant."]);

        return await CreateSiteCoreAsync(request, isFirstSite: false, cancellationToken);
    }

    /// <summary>Removes all tenant rows; schema and migrations are kept.</summary>
    public async Task<SetupStatus> WipeAllTenantDataAsync(CancellationToken cancellationToken = default)
    {
        const string truncateSql = """
            TRUNCATE TABLE
                "OrderItems",
                "Orders",
                "ProductIngredients",
                "Products",
                "InventoryItems",
                "Tables",
                "EmployeeAttendances",
                "SalaryAdvances",
                "PayrollPaymentRecords",
                "Employees",
                "Transactions",
                "CustomerProfiles",
                "ReservationEngagements",
                "Reservations",
                "PlacementUnits",
                "WaitlistEntries",
                "SharedOrderDrafts",
                "TabletSessions",
                "SyncOutbox",
                "PublicMenuAssets",
                "PublicMenuSettings",
                "AttendanceDayValidations",
                "Restaurants"
            RESTART IDENTITY CASCADE;
            """;

        await db.Database.ExecuteSqlRawAsync(truncateSql, cancellationToken);
        return await GetStatusAsync(cancellationToken);
    }

    private async Task<SiteSetupResult> CreateSiteCoreAsync(
        SiteSetupCommand request,
        bool isFirstSite,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return SiteSetupResult.Fail(errors);

        var slug = RestaurantSlug.Normalize(request.Slug, request.RestaurantName);
        if (!RestaurantSlug.IsValid(slug))
            return SiteSetupResult.Fail(["Slug must be 2–64 characters: lowercase letters, numbers, and hyphens only."]);

        var domain = NormalizeOptionalDomain(request.CustomDomain);
        if (domain is not null && await DomainInUseAsync(domain, cancellationToken))
            return SiteSetupResult.Fail(["That custom domain is already registered to another restaurant."]);

        if (await SlugInUseAsync(slug, cancellationToken))
            return SiteSetupResult.Fail(["That slug is already in use."]);

        var signInId = request.AdminSignInId.Trim();
        var adminName = string.IsNullOrWhiteSpace(request.AdminName)
            ? $"{request.RestaurantName.Trim()} Admin"
            : request.AdminName.Trim();
        var lang = NormalizeLanguage(request.PreferredLanguage);
        var uniqueSuffix = slug.Replace('-', '_').ToUpperInvariant();
        if (uniqueSuffix.Length > 24)
            uniqueSuffix = uniqueSuffix[..24];

        if (db.Database.IsRelational())
        {
            return await DatabaseResilientTransaction.ExecuteAsync(
                db,
                (request, slug, domain, signInId, adminName, lang, uniqueSuffix, isFirstSite),
                async (context, state, ct) =>
                {
                    await using var tx = await context.Database.BeginTransactionAsync(ct);
                    try
                    {
                        var created = await PersistSiteAsync(
                            state.request,
                            state.slug,
                            state.domain,
                            state.signInId,
                            state.adminName,
                            state.lang,
                            state.uniqueSuffix,
                            state.isFirstSite,
                            ct);
                        await tx.CommitAsync(ct);
                        return SiteSetupResult.Ok(created);
                    }
                    catch
                    {
                        await tx.RollbackAsync(ct);
                        throw;
                    }
                },
                cancellationToken);
        }

        var site = await PersistSiteAsync(
            request, slug, domain, signInId, adminName, lang, uniqueSuffix, isFirstSite, cancellationToken);
        return SiteSetupResult.Ok(site);
    }

    private async Task<CreatedSite> PersistSiteAsync(
        SiteSetupCommand request,
        string slug,
        string? domain,
        string signInId,
        string adminName,
        string lang,
        string uniqueSuffix,
        bool isFirstSite,
        CancellationToken cancellationToken)
    {
        var restaurant = new Restaurant
        {
            UniqueId = $"REST-{uniqueSuffix}",
            Name = request.RestaurantName.Trim(),
            Slug = slug,
            CustomDomain = domain,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync(cancellationToken);

        db.PublicMenuSettings.Add(new PublicMenuSetting
        {
            RestaurantId = restaurant.Id,
            Key = "default",
            RestaurantName = restaurant.Name,
            WebsiteDomain = domain ?? string.Empty,
            AdminWebSignInId = signInId,
            AdminWebPin = request.AdminPin.Trim(),
            UpdatedAtUtc = DateTime.UtcNow
        });

        var admin = new Employee
        {
            RestaurantId = restaurant.Id,
            UniqueId = isFirstSite ? "EMP-SETUP-ADMIN-001" : $"EMP-ADMIN-{uniqueSuffix}",
            SignInId = signInId,
            Name = adminName,
            Role = "Admin",
            PinCode = EmployeePinHasher.HashForStorage(request.AdminPin),
            EmploymentStatus = "Active",
            JoinDate = DateTime.UtcNow,
            PreferredLanguage = lang,
            ProfileImagePath = string.Empty,
            PhoneNumber = string.Empty,
            Notes = isFirstSite
                ? "Created by first-site setup."
                : "Created by platform new-site setup."
        };
        db.Employees.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        return new CreatedSite(
            restaurant.Id,
            restaurant.UniqueId,
            restaurant.Slug,
            restaurant.CustomDomain,
            admin.Id,
            admin.UniqueId,
            admin.Name,
            admin.SignInId,
            admin.Role);
    }

    private static List<string> Validate(SiteSetupCommand request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.RestaurantName) || request.RestaurantName.Trim().Length < 2)
            errors.Add("Restaurant name is required (at least 2 characters).");
        if (string.IsNullOrWhiteSpace(request.AdminSignInId))
            errors.Add("Admin sign-in ID is required.");
        else if (request.AdminSignInId.Trim().Length > 32)
            errors.Add("Admin sign-in ID must be 32 characters or fewer.");

        var pin = (request.AdminPin ?? string.Empty).Trim();
        if (pin.Length < 4)
            errors.Add("Admin PIN must be at least 4 characters.");
        if (pin.Length > 32)
            errors.Add("Admin PIN must be 32 characters or fewer.");

        return errors;
    }

    private static string? NormalizeOptionalDomain(string? customDomain)
    {
        if (string.IsNullOrWhiteSpace(customDomain))
            return null;

        var raw = customDomain.Trim();
        if (raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            raw = raw[8..];
        else if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            raw = raw[7..];

        var slash = raw.IndexOf('/');
        if (slash >= 0)
            raw = raw[..slash];

        var normalized = RestaurantHostNormalizer.NormalizeHost(raw);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string NormalizeLanguage(string? lang)
    {
        var l = (lang ?? "en").Trim().ToLowerInvariant();
        return l is "fr" or "en" ? l : "en";
    }

    private Task<bool> SlugInUseAsync(string slug, CancellationToken cancellationToken) =>
        db.Restaurants.IgnoreQueryFilters()
            .AnyAsync(r => r.Slug == slug, cancellationToken);

    private async Task<bool> DomainInUseAsync(string domain, CancellationToken cancellationToken)
    {
        var rows = await db.Restaurants.IgnoreQueryFilters()
            .Where(r => r.CustomDomain != null && r.CustomDomain != "")
            .Select(r => r.CustomDomain!)
            .ToListAsync(cancellationToken);
        return rows.Any(d => RestaurantHostNormalizer.NormalizeHost(d) == domain);
    }
}

public sealed record SiteSetupCommand(
    string RestaurantName,
    string? Slug,
    string? CustomDomain,
    string AdminSignInId,
    string AdminPin,
    string? AdminName,
    string? PreferredLanguage);

public sealed record SetupStatus(bool SetupRequired, int RestaurantCount, string Message);

public sealed record CreatedSite(
    int RestaurantId,
    string RestaurantUniqueId,
    string Slug,
    string? CustomDomain,
    int EmployeeId,
    string EmployeeUniqueId,
    string Name,
    string SignInId,
    string Role);

public sealed class SiteSetupResult
{
    public bool Success { get; init; }
    public CreatedSite? Site { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static SiteSetupResult Ok(CreatedSite site) =>
        new() { Success = true, Site = site };

    public static SiteSetupResult Fail(IReadOnlyList<string> errors) =>
        new() { Success = false, Errors = errors };
}
