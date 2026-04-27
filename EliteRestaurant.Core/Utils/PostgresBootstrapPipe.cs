using System.Linq;

namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Parses first-run / retry database bootstrap strings of the form
/// <c>host|port|database|username|password</c>. The password segment may contain <c>|</c> characters;
/// everything after the fourth <c>|</c> is joined back into the password.
/// </summary>
public static class PostgresBootstrapPipe
{
    public static bool TryParse(
        string input,
        out string host,
        out int port,
        out string database,
        out string user,
        out string password)
    {
        host = database = user = password = string.Empty;
        port = 5432;
        var parts = input.Split('|');
        if (parts.Length < 5)
            return false;
        host = parts[0].Trim();
        if (!int.TryParse(parts[1].Trim(), out port) || port <= 0)
            port = 5432;
        database = parts[2].Trim();
        user = parts[3].Trim();
        password = string.Join("|", parts.Skip(4)).Trim();
        return !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(database) && !string.IsNullOrWhiteSpace(user);
    }
}
