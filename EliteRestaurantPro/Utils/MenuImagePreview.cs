using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.Utils;

internal static class MenuImagePreview
{
    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// Cached decoded bitmaps keyed by full URL. Cleared when the menu reloads so updated photos refetch.
    /// </summary>
    private static readonly ConcurrentDictionary<string, ImageSource?> RemoteByUrl = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EliteRestaurantPro/1.0 (menu photo)");
        return client;
    }

    internal static string GetProductPhotoAssetUrl(int productId)
    {
        var baseUrl = CloudEndpoints.NormalizeApiBaseUrl(SettingsManager.Load().CloudApi.BaseUrl);
        return $"{baseUrl.TrimEnd('/')}/api/public/menu/assets/product/{productId}";
    }

    internal static void ClearRemoteImageCache() => RemoteByUrl.Clear();

    /// <summary>
    /// Warms the remote image cache (parallel). Call before binding product cards so the UI thread is not blocked.
    /// </summary>
    internal static void PrefetchProductPhotoUrls(IEnumerable<int> productIds)
    {
        Parallel.ForEach(
            productIds.Where(id => id > 0).Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = 6 },
            id => _ = TryLoadFromPathOrUrl(GetProductPhotoAssetUrl(id)));
    }

    internal static ImageSource? TryLoadFromPathOrUrl(string? pathOrUrl)
    {
        var raw = (pathOrUrl ?? string.Empty).Trim();
        if (raw.Length == 0)
            return null;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            if (!File.Exists(raw))
                return null;
            uri = new Uri(raw, UriKind.Absolute);
        }
        else if (uri.IsFile && !File.Exists(uri.LocalPath))
        {
            return null;
        }

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            return TryLoadRemoteHttp(uri);

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = uri;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? TryLoadRemoteHttp(Uri uri)
    {
        var key = uri.ToString();
        return RemoteByUrl.GetOrAdd(key, static (_, u) => DownloadAndDecode((Uri)u!), uri);
    }

    private static ImageSource? DownloadAndDecode(Uri uri)
    {
        try
        {
            var bytes = Http.GetByteArrayAsync(uri).ConfigureAwait(false).GetAwaiter().GetResult();
            if (bytes.Length == 0)
                return null;

            return DecodeBitmapFromBytes(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? DecodeBitmapFromBytes(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch
        {
            try
            {
                using var ms = new MemoryStream(bytes, writable: false);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
