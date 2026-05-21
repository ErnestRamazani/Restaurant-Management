using System.Security.Cryptography;
using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

public static class ReservationConfirmationCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static async Task<string> AllocateUniqueAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var code = Generate();
            var exists = await db.ReservationEngagements.AsNoTracking()
                .AnyAsync(e => e.ConfirmationCode == code, cancellationToken);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Could not allocate a unique reservation confirmation code.");
    }

    public static string Generate()
    {
        Span<char> buffer = stackalloc char[6];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(buffer);
    }
}
