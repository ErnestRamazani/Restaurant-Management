using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;

namespace EliteRestaurant.Api.Branding;

/// <summary>
/// Resolves the restaurant logo file used for public web and staff portal image endpoints.
/// </summary>
/// <remarks>
/// <para><b>Customer website / public menu</b> (<c>/api/public/menu/assets/logo</c>) and
/// <b>server portal</b> (<c>/api/server/assets/restaurant-logo</c>) use the same precedence:</para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Repository assets (primary for the website when present on disk):</b>
/// file under <c>assets/images/logo</c> — see <see cref="CanonicalLogoFileNames"/>.
/// If none of those exist, the first image file in that directory (alphabetically) is used so
/// drop-in files keep working before rename to a canonical name.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Database:</b> <c>PublicMenuAssets</c> row <c>Key == "logo"</c> (cloud profile upload from
/// desktop admin). Used when no suitable on-disk file is found.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Local settings:</b> <c>BusinessProfile.LogoPath</c> absolute path (legacy / desktop local file).
/// </description>
/// </item>
/// </list>
/// On-disk repo logos take precedence so the committed <c>assets/images/logo</c> folder is the
/// source of truth for web branding when those files exist; DB still stores operator uploads when
/// no repo file is deployed.
/// </remarks>
public static class RestaurantWebLogoResolver
{
    /// <summary>
    /// Preferred filenames under <c>assets/images/logo</c> (first match wins, in order).
    /// </summary>
    public static readonly string[] CanonicalLogoFileNames =
    [
        "restaurant-logo.svg",
        "restaurant-logo.png",
        "logo.svg",
        "logo.png"
    ];

    private static readonly string[] FallbackImageExtensions = [".svg", ".png", ".webp", ".jpg", ".jpeg", ".gif"];

    /// <summary>
    /// Returns an absolute path to a repo logo file, or <c>null</c> if none exists.
    /// </summary>
    public static string? TryResolveRepoLogoPath(IWebHostEnvironment env)
    {
        foreach (var dir in GetCandidateLogoDirectories(env))
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (var name in CanonicalLogoFileNames)
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full))
                    return full;
            }

            var any = TryFirstImageFileInDirectory(dir);
            if (any is not null)
                return any;
        }

        return null;
    }

    /// <summary>
    /// Maps a file path to a content-type for <see cref="M:Microsoft.AspNetCore.Mvc.ControllerBase.File(System.Byte[],System.String)"/>.
    /// </summary>
    public static string GetContentTypeForPath(string absolutePath)
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(absolutePath, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private static string? TryFirstImageFileInDirectory(string dir)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(dir);
        }
        catch
        {
            return null;
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            var ext = Path.GetExtension(path);
            foreach (var ok in FallbackImageExtensions)
            {
                if (string.Equals(ext, ok, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Directories checked, in order: sibling <c>../assets/images/logo</c> (dev),
    /// <c>ContentRoot/assets/images/logo</c>, output-directory <c>assets/images/logo</c> (publish).
    /// </summary>
    private static IEnumerable<string> GetCandidateLogoDirectories(IWebHostEnvironment env)
    {
        var contentRoot = env.ContentRootPath;
        if (!string.IsNullOrEmpty(contentRoot))
        {
            yield return Path.GetFullPath(Path.Combine(contentRoot, "..", "assets", "images", "logo"));
            yield return Path.Combine(contentRoot, "assets", "images", "logo");
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
            yield return Path.Combine(baseDir, "assets", "images", "logo");
    }
}
