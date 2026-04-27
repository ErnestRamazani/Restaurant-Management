using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EliteRestaurantPro.Utils;

/// <summary>Builds a base URL for customer-menu QR links so phones (not the server PC) can open the app.</summary>
public static class PublicMenuUrlHelper
{
    /// <summary>Production API (and static menu from wwwroot) listen here.</summary>
    public const int DefaultApiHttpPort = 5223;

    /// <summary>Vite dev server with <c>host: true</c> (LAN phones load SPA here; <c>/api</c> is proxied to the API).</summary>
    public const int ViteDevMenuPort = 5173;

    /// <summary>Port embedded in auto-generated QR / phone-friendly URLs: Vite in DEBUG, API in release.</summary>
    public static int QrBasePort { get; } = GetQrDefaultPort();

    private static int GetQrDefaultPort() =>
#if DEBUG
        ViteDevMenuPort;
#else
        DefaultApiHttpPort;
#endif

    /// <summary>
    /// Uses the OS’s chosen outbound path (does not require traffic to leave the machine) when interface enumeration is inconclusive.
    /// </summary>
    public static string? GetLocalLanIpViaDefaultRoute()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
                return endPoint.Address.ToString();
        }
        catch
        {
            // ignored
        }

        return null;
    }

    /// <summary>
    /// Picks a LAN IPv4 that customer phones on Wi-Fi are likely to share with this PC. Prefers
    /// <c>192.168.x.x</c> and Wi-Fi/ethernet interfaces, and de-prioritises <c>10.x</c> from
    /// VPN/bridge adapters that often win the "first private IP" and OS default-route tricks.
    /// </summary>
    public static string? GetPreferredLanIPv4()
    {
        var candidates = new List<(string Ip, int Score)>(8);
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

                var typeBonus = ni.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => 50,
                    NetworkInterfaceType.Ethernet => 40,
                    _ => 0
                };
                var virtualPenalty = IsProbablyVirtualOrTunnelAdapter(ni) ? -200 : 0;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    var s = ua.Address.ToString();
                    if (s.Length == 0 || s == "0.0.0.0") continue;
                    if (s.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                    if (!IsPrivateUnicastRfc1918(s)) continue;

                    // Strong preference: home Wi-Fi is almost always 192.168.0.0/16. VPN/VM bridges
                    // that duplicate a "10." address in Vite are common and unreachable from a phone
                    // on the same 192.168 AP.
                    var prefix = PrivateRfc1918PrefixWeight(s) + typeBonus + virtualPenalty;
                    candidates.Add((s, prefix));
                }
            }

            if (candidates.Count > 0)
            {
                return candidates
                    .OrderByDescending(c => c.Score)
                    .ThenBy(c => c.Ip, StringComparer.Ordinal)
                    .First()
                    .Ip;
            }
        }
        catch
        {
            // fall through
        }

        return GetLocalLanIpViaDefaultRoute();
    }

    private static int PrivateRfc1918PrefixWeight(string s)
    {
        if (s.StartsWith("192.168.", StringComparison.Ordinal)) return 1000;
        if (s.StartsWith("172.", StringComparison.Ordinal) && IsIn172Rfc1918(s)) return 500;
        if (s.StartsWith("10.", StringComparison.Ordinal)) return 200;
        return 0;
    }

    private static bool IsIn172Rfc1918(string s)
    {
        // 172.16.0.0 – 172.31.255.255
        if (!s.StartsWith("172.", StringComparison.Ordinal)) return false;
        var second = s.IndexOf('.', 4);
        if (second < 0) return false;
        if (!int.TryParse(s.AsSpan(4, second - 4), out var octet1)) return false;
        return octet1 is >= 16 and <= 31;
    }

    private static bool IsPrivateUnicastRfc1918(string s) =>
        s.StartsWith("192.168.", StringComparison.Ordinal) ||
        s.StartsWith("10.", StringComparison.Ordinal) ||
        (s.StartsWith("172.", StringComparison.Ordinal) && IsIn172Rfc1918(s));

    private static bool IsProbablyVirtualOrTunnelAdapter(NetworkInterface ni)
    {
        var text = (ni.Name + " " + ni.Description).ToLowerInvariant();
        string[] hints =
        {
            "vethernet", "hyper-v", "vbox", "virtualbox", "vmware", "virtual ", "vnet",
            "wsl", "wsl2", "docker", "tun", "tap-windows", "zerotier", "tailscale",
            "openvpn", "nordlynx", "windscribe", "wireguard", "wg ",
            "npcap", "nmap ", "cisco anyconnect", "fortinet", "globalprotect",
        };
        foreach (var h in hints)
        {
            if (text.Contains(h, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <param name="port">Usually <see cref="QrBasePort"/> (5173 in DEBUG, 5223 in release) for correct QR in dev vs production.</param>
    public static string? SuggestBaseUrlForPhones(int? port = null)
    {
        var p = port ?? QrBasePort;
        var ip = GetPreferredLanIPv4();
        if (string.IsNullOrEmpty(ip)) return null;
        return $"http://{ip}:{p}";
    }

    /// <summary>
    /// If <paramref name="configured"/> is a real LAN/public URL, use it. If empty or <c>localhost</c> / <c>127.0.0.1</c>,
    /// substitute auto-detected LAN + <see cref="QrBasePort"/> so phones on Wi-Fi can reach the menu.
    /// </summary>
    public static string ResolveQrBaseUrl(string? configured)
    {
        var t = (configured ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(t) && !LooksLikeLocalHostOnly(t))
            return t;
        return SuggestBaseUrlForPhones() ?? t;
    }

    public static bool LooksLikeLocalHostOnly(string? baseUrl) =>
        !string.IsNullOrEmpty(baseUrl) &&
        (baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
         baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase));
}
