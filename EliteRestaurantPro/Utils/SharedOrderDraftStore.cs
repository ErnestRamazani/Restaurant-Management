using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.Utils;

public sealed record SharedDraftRow(string Id, string Label, string PayloadJson, DateTime UpdatedAtUtc);

public static class SharedOrderDraftStore
{
    private const string ServerPortal = "Server";

    public static IReadOnlyList<SharedDraftRow> ListServerDrafts(int employeeId)
    {
        using var db = new AppDbContext();
        return db.SharedOrderDrafts
            .AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && d.Portal == ServerPortal)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Select(d => new SharedDraftRow(d.UniqueId, d.DraftLabel, d.PayloadJson, d.UpdatedAtUtc))
            .Take(50)
            .ToList();
    }

    public static SharedDraftRow SaveServerDraft(int employeeId, string employeeName, string label, string payloadJson)
    {
        var now = DateTime.UtcNow;
        var safeLabel = string.IsNullOrWhiteSpace(label) ? $"Draft {now:yyyy-MM-dd HH:mm:ss}" : label.Trim();
        var safePayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;

        using var db = new AppDbContext();
        var entity = new SharedOrderDraft
        {
            UniqueId = Guid.NewGuid().ToString("N"),
            EmployeeId = employeeId,
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? $"Employee #{employeeId}" : employeeName.Trim(),
            Portal = ServerPortal,
            DraftLabel = safeLabel,
            PayloadJson = safePayload,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.SharedOrderDrafts.Add(entity);
        db.SaveChanges();
        return new SharedDraftRow(entity.UniqueId, entity.DraftLabel, entity.PayloadJson, entity.UpdatedAtUtc);
    }

    public static bool DeleteServerDraft(int employeeId, string draftUniqueId)
    {
        if (string.IsNullOrWhiteSpace(draftUniqueId))
            return false;

        using var db = new AppDbContext();
        var row = db.SharedOrderDrafts
            .FirstOrDefault(d => d.EmployeeId == employeeId && d.Portal == ServerPortal && d.UniqueId == draftUniqueId);
        if (row is null)
            return false;

        db.SharedOrderDrafts.Remove(row);
        db.SaveChanges();
        return true;
    }
}
