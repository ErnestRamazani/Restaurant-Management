using System.Text.Json;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Utils;

public sealed record SharedDraftRow(
    string Id,
    string Label,
    string PayloadJson,
    DateTime UpdatedAtUtc,
    int TableId = 0,
    bool IsCustomerDraft = false);

public sealed class SharedOrderDraftService(AppDbContext db)
{
    public const string ServerPortal = "Server";

    private const int MaxDraftsPerEmployeePortal = 30;

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
            if (root.TryGetProperty("TableId", out var d) && d.ValueKind == JsonValueKind.Number)
                return d.GetInt32();
        }
        catch
        {
            /* invalid snapshot */
        }

        return 0;
    }

    public SharedDraftRow? GetServerDraft(
        int employeeId,
        string draftUniqueId,
        bool restrictCustomerToAssignedServer = false)
    {
        if (string.IsNullOrWhiteSpace(draftUniqueId))
            return null;

        var row = db.SharedOrderDrafts.AsNoTracking()
            .FirstOrDefault(d => d.Portal == ServerPortal && d.UniqueId == draftUniqueId);

        if (row is null)
            return null;

        if (row.EmployeeId == employeeId)
            return new SharedDraftRow(row.UniqueId, row.DraftLabel, row.PayloadJson, row.UpdatedAtUtc, row.TableId, false);

        if (row.EmployeeId != 0)
            return null;

        if (restrictCustomerToAssignedServer)
        {
            var t = db.Tables.AsNoTracking().FirstOrDefault(x => x.Id == row.TableId);
            if (t is not null && t.AssignedServerId.HasValue && t.AssignedServerId != employeeId)
                return null;
        }

        return new SharedDraftRow(row.UniqueId, row.DraftLabel, row.PayloadJson, row.UpdatedAtUtc, row.TableId, true);
    }

    public IReadOnlyList<SharedDraftRow> ListServerDrafts(
        int employeeId,
        int selectedTableId,
        bool restrictCustomerDraftToAssignedServer = false) =>
        db.SharedOrderDrafts
            .AsNoTracking()
            .Where(d =>
                d.Portal == ServerPortal
                && (
                    (d.EmployeeId == employeeId
                     && (selectedTableId <= 0 || d.TableId == 0 || d.TableId == selectedTableId))
                    || (d.EmployeeId == 0
                        && d.TableId > 0
                        && (selectedTableId <= 0 || d.TableId == selectedTableId)
                        && (!restrictCustomerDraftToAssignedServer
                            || db.Tables.Any(t =>
                                t.Id == d.TableId
                                && (t.AssignedServerId == null || t.AssignedServerId == employeeId))
                        )
                    )))
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Select(d => new SharedDraftRow(
                d.UniqueId,
                d.DraftLabel,
                d.PayloadJson,
                d.UpdatedAtUtc,
                d.TableId,
                d.EmployeeId == 0))
            .Take(50)
            .ToList();

    public SharedDraftRow SaveServerDraft(int employeeId, string employeeName, string label, string payloadJson, int tableId = 0)
    {
        var now = DateTime.UtcNow;
        var safeLabel = string.IsNullOrWhiteSpace(label) ? $"Draft {now:yyyy-MM-dd HH:mm:ss}" : label.Trim();
        var safePayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        var safeName = string.IsNullOrWhiteSpace(employeeName) ? $"Employee #{employeeId}" : employeeName.Trim();
        var rowTableId = tableId;
        if (rowTableId == 0)
            rowTableId = ParseTableIdFromSnapshotJson(safePayload);

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
        EnforceMaxDraftsPerEmployeePortal(employeeId);
        return new SharedDraftRow(
            entity.UniqueId,
            entity.DraftLabel,
            entity.PayloadJson,
            entity.UpdatedAtUtc,
            entity.TableId,
            entity.EmployeeId == 0);
    }

    public bool DeleteServerDraft(
        int employeeId,
        string draftUniqueId,
        int selectedTableId = 0,
        bool restrictCustomerDeleteToAssignedServer = false)
    {
        if (string.IsNullOrWhiteSpace(draftUniqueId))
            return false;

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

    public int PurgeDraftsOlderThan(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        return db.SharedOrderDrafts.Where(d => d.UpdatedAtUtc < cutoff).ExecuteDelete();
    }

    private void EnforceMaxDraftsPerEmployeePortal(int employeeId)
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
