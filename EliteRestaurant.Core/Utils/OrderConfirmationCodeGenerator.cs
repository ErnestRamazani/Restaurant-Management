using System.Globalization;
using System.Security.Cryptography;
using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

public static class OrderConfirmationCodeGenerator
{
    public static async Task<string> AllocateUniqueAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var code = Generate();
            var exists = await db.Orders.AsNoTracking()
                .AnyAsync(o => o.ConfirmationCode == code, cancellationToken);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Could not allocate a unique order confirmation code.");
    }

    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
