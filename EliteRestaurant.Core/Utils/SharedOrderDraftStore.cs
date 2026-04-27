using System.Text.Json;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

public sealed record SharedDraftRow(string Id, string Label, string PayloadJson, DateTime UpdatedAtUtc);

public static class SharedOrderDraftStore
{
    public const string ServerPortal = "Server";

    /// <summary>Max drafts stored per employee per portal after each save.</summary>
    private const int MaxDraftsPerEmployeePortal = 30;

    /// <summary>Reads table id from server portal / WPF draft JSON (camelCase or PascalCase, or cashier <c>tableId</c>).</summary>
    public static int ParseTableIdFromSnapshotJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("selectedTableId", out var a) && a.ValueKind == JsonValueKind.Number)
                return a.GetInt32();
            if (root.TryGetProperty("SelectedTableId", out var b) && b.ValueKind == JsonValueKind.Number)
                return b.GetInt32();
            if (root.TryGetProperty("tableId", out var c) && c.ValueKind == JsonValueKind.Number)
                return c.GetInt32();
        }
        catch
        {
            /* invalid snapshot */
        }

        return 0;
    }

    /// <param name="selectedTableId">Table chosen in create-order / server UI. Customer drafts (EmployeeId 0) only list when <see cref="TableId"/> matches.</param>
    /// <param name="restrictCustomerDraftToAssignedServer">When true (server tablet / web Server role), customer drafts are listed only for tables the employee is assigned to (or unassigned table).</param>
    public static IReadOnlyList<SharedDraftRow> ListServerDrafts(
        int employeeId,
        int selectedTableId,
        bool restrictCustomerDraftToAssignedServer = false)
    {
        using var db = new AppDbContext();
        // Own saved drafts: show for current table, or any table when row has TableId 0 (legacy or unknown).
        // Customer drafts (0): only when TableId == selected table, optionally restricted by assignment.
        return db.SharedOrderDrafts
            .AsNoTracking()
            .Where(d =>
                d.Portal == ServerPortal
                && (
                    (d.EmployeeId == employeeId
                     && (selectedTableId <= 0 || d.TableId == 0 || d.TableId == selectedTableId))
                    || (d.EmployeeId == 0
                        && selectedTableId > 0
                        && d.TableId == selectedTableId
                        && (!restrictCustomerDraftToAssignedServer
                            || db.Tables.Any(t =>
                                t.Id == d.TableId
                                && (t.AssignedServerId == null || t.AssignedServerId == employeeId))
                        )
                    )))
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Select(d => new SharedDraftRow(d.UniqueId, d.DraftLabel, d.PayloadJson, d.UpdatedAtUtc))
            .Take(50)
            .ToList();
    }

    public static SharedDraftRow SaveServerDraft(int employeeId, string employeeName, string label, string payloadJson, int tableId = 0)
    {
        var now = DateTime.UtcNow;
        var safeLabel = string.IsNullOrWhiteSpace(label) ? $"Draft {now:yyyy-MM-dd HH:mm:ss}" : label.Trim();
        var safePayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        var safeName = string.IsNullOrWhiteSpace(employeeName) ? $"Employee #{employeeId}" : employeeName.Trim();
        var rowTableId = tableId;
        if (rowTableId == 0)
            rowTableId = ParseTableIdFromSnapshotJson(safePayload);

        using var db = new AppDbContext();
        var primaryId = db.SharedOrderDrafts.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && d.Portal == ServerPortal && d.DraftLabel == safeLabel)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Select(d => d.Id)
            .FirstOrDefault();

        SharedOrderDraft entity;
        if (primaryId != 0)
        {
            db.SharedOrderDrafts
                .Where(d =>
                    d.EmployeeId == employeeId
                    && d.Portal == ServerPortal
                    && d.DraftLabel == safeLabel
                    && d.Id != primaryId)
                .ExecuteDelete();
            entity = db.SharedOrderDrafts.Single(d => d.Id == primaryId);
            entity.PayloadJson = safePayload;
            entity.UpdatedAtUtc = now;
            entity.EmployeeName = safeName;
            if (rowTableId != 0)
                entity.TableId = rowTableId;
        }
        else
        {
            entity = new SharedOrderDraft
            {
                UniqueId = Guid.NewGuid().ToString("N"),
                EmployeeId = employeeId,
                EmployeeName = safeName,
                Portal = ServerPortal,
                DraftLabel = safeLabel,
                PayloadJson = safePayload,
                TableId = rowTableId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.SharedOrderDrafts.Add(entity);
        }

        db.SaveChanges();
        EnforceMaxDraftsPerEmployeePortal(db, employeeId);
        return new SharedDraftRow(entity.UniqueId, entity.DraftLabel, entity.PayloadJson, entity.UpdatedAtUtc);
    }

    /// <param name="selectedTableId">When deleting a customer draft (EmployeeId 0), must match <see cref="SharedOrderDraft.TableId"/>.</param>
    public static bool DeleteServerDraft(
        int employeeId,
        string draftUniqueId,
        int selectedTableId = 0,
        bool restrictCustomerDeleteToAssignedServer = false)
    {
        if (string.IsNullOrWhiteSpace(draftUniqueId))
            return false;

        using var db = new AppDbContext();
        var row = db.SharedOrderDrafts
            .FirstOrDefault(d => d.Portal == ServerPortal && d.UniqueId == draftUniqueId);

        if (row is null)
            return false;

        if (row.EmployeeId == employeeId)
        {
            db.SharedOrderDrafts.Remove(row);
            db.SaveChanges();
            return true;
        }

        if (row.EmployeeId != 0)
            return false;

        if (selectedTableId <= 0 || row.TableId != selectedTableId)
            return false;

        if (restrictCustomerDeleteToAssignedServer)
        {
            var t = db.Tables.AsNoTracking().FirstOrDefault(x => x.Id == row.TableId);
            if (t is not null && t.AssignedServerId.HasValue && t.AssignedServerId != employeeId)
                return false;
        }

        db.SharedOrderDrafts.Remove(row);
        db.SaveChanges();
        return true;
    }

    /// <summary>Removes drafts not updated within <paramref name="maxAge"/> (UTC).</summary>
    public static int PurgeDraftsOlderThan(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        using var db = new AppDbContext();
        return db.SharedOrderDrafts.Where(d => d.UpdatedAtUtc < cutoff).ExecuteDelete();
    }

    private static void EnforceMaxDraftsPerEmployeePortal(AppDbContext db, int employeeId)
    {
        var keepIds = db.SharedOrderDrafts
            .AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && d.Portal == ServerPortal)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ThenByDescending(d => d.Id)
            .Take(MaxDraftsPerEmployeePortal)
            .Select(d => d.Id)
            .ToList();

        if (keepIds.Count == 0)
            return;

        db.SharedOrderDrafts
            .Where(d => d.EmployeeId == employeeId && d.Portal == ServerPortal && !keepIds.Contains(d.Id))
            .ExecuteDelete();
    }
}
