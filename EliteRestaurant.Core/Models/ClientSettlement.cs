namespace EliteRestaurant.Core.Models;

/// <summary>Persisted on <see cref="OrderRecord.ClientSettlement"/>.</summary>
public static class ClientSettlement
{
    public const string None = "None";
    public const string PaidAtCompletion = "PaidAtCompletion";
    public const string OnAccount = "OnAccount";

    public static bool IsOnAccount(string? value) =>
        string.Equals(value, OnAccount, StringComparison.OrdinalIgnoreCase);

    public static bool IsPaidAtCompletion(string? value) =>
        string.Equals(value, PaidAtCompletion, StringComparison.OrdinalIgnoreCase);
}
