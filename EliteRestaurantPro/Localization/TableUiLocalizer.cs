using System.Globalization;
using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.Localization;

public static class TableUiLocalizer
{
    public static void Apply(Table table)
    {
        table.DisplayStatus = AdminTextLocalizer.TranslateTableStatus(table.Status);
        table.DisplayCapacityText = Loc.Admin("tblCapacityPrefix", "Capacity {{count}}",
            new Dictionary<string, string> { ["count"] = table.Capacity.ToString(CultureInfo.InvariantCulture) });
        table.DisplayTableIdLine = Loc.Admin("tblTableIdPrefix", "Table ID") + " " + table.TableNumber;
        var serverName = table.AssignedServer?.Name;
        table.DisplayServerLine = Loc.Admin("tblServerPrefix", "Server:") + " " +
            (string.IsNullOrWhiteSpace(serverName)
                ? Loc.Admin("tblUnassigned", "Unassigned")
                : serverName);
    }

    public static void ApplyAll(IEnumerable<Table> tables)
    {
        foreach (var table in tables)
            Apply(table);
    }
}
