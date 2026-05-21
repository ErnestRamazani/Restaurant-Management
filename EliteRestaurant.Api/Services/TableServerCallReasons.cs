namespace EliteRestaurant.Api.Services;

public static class TableServerCallReasons
{
    public const string BringBill = "bring_bill";
    public const string RefillDrink = "refill_drink";
    public const string PackLeftover = "pack_leftover";
    public const string ExtraCutlery = "extra_cutlery";
    public const string ProblemFood = "problem_food";
    public const string Other = "other";

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [BringBill] = "Bring the bill",
        [RefillDrink] = "Refill drink",
        [PackLeftover] = "Pack leftover",
        [ExtraCutlery] = "Missing items / Extra cutlery",
        [ProblemFood] = "Problem with food",
        [Other] = "Other / Call server"
    };

    public static IReadOnlyList<(string Code, string Label)> All { get; } =
        Labels.Select(kv => (kv.Key, kv.Value)).ToList();

    public static bool TryGetLabel(string? code, out string label)
    {
        if (!string.IsNullOrWhiteSpace(code) && Labels.TryGetValue(code.Trim(), out label!))
            return true;
        label = string.Empty;
        return false;
    }
}
