using System.Reflection;
using System.Text.Json;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/sync")]
[Authorize(Policy = "OperationalWrite")]
public sealed class AdminSyncController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, Type> EntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(Product)] = typeof(Product),
        [nameof(ProductIngredient)] = typeof(ProductIngredient),
        [nameof(Employee)] = typeof(Employee),
        [nameof(Table)] = typeof(Table),
        [nameof(ReservationBooking)] = typeof(ReservationBooking),
        [nameof(InventoryItem)] = typeof(InventoryItem),
        [nameof(EmployeeAttendance)] = typeof(EmployeeAttendance),
        [nameof(SalaryAdvance)] = typeof(SalaryAdvance),
        [nameof(PayrollPaymentRecord)] = typeof(PayrollPaymentRecord),
        [nameof(OrderRecord)] = typeof(OrderRecord),
        [nameof(OrderItem)] = typeof(OrderItem),
        [nameof(MoneyTransaction)] = typeof(MoneyTransaction),
        [nameof(CustomerProfile)] = typeof(CustomerProfile),
        [nameof(WaitlistEntry)] = typeof(WaitlistEntry),
        [nameof(SharedOrderDraft)] = typeof(SharedOrderDraft),
        [nameof(TabletSession)] = typeof(TabletSession),
        [nameof(AttendanceDayValidation)] = typeof(AttendanceDayValidation),
        [nameof(PublicMenuAsset)] = typeof(PublicMenuAsset)
    };

    [HttpPost]
    public async Task<ActionResult<AdminSyncBatchResponse>> Sync(AdminSyncBatchRequest request, CancellationToken cancellationToken)
    {
        var results = new List<AdminSyncOperationResultDto>();

        foreach (var operation in request.Operations)
        {
            if (await db.SyncOutbox.AsNoTracking().AnyAsync(
                    o => o.IdempotencyKey == operation.IdempotencyKey && o.Status == "Synced",
                    cancellationToken))
            {
                results.Add(Success(operation, "Already applied."));
                continue;
            }

            if (!EntityTypes.TryGetValue(operation.EntityName, out var entityType))
            {
                results.Add(Failure(operation, "Unsupported entity type."));
                continue;
            }

            try
            {
                var isTable = string.Equals(operation.EntityName, nameof(Table), StringComparison.OrdinalIgnoreCase);

                if (operation.Operation.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (isTable)
                    {
                        var incoming = operation.Payload.Deserialize(typeof(Table), JsonOptions) as Table;
                        var existingTable = await FindExistingAsync(typeof(Table), incoming!, cancellationToken) as Table;
                        if (existingTable is not null)
                            await PlacementUnitProvisioner.RemoveForTableAsync(db, existingTable.Id, cancellationToken);
                    }

                    await DeleteAsync(entityType, operation.Payload, cancellationToken);
                }
                else
                {
                    await UpsertAsync(entityType, operation.Payload, cancellationToken);
                }

                db.SyncOutbox.Add(new SyncOutbox
                {
                    IdempotencyKey = operation.IdempotencyKey,
                    EntityName = operation.EntityName,
                    Operation = operation.Operation,
                    PayloadJson = operation.Payload.GetRawText(),
                    Status = "Synced",
                    Attempts = 1,
                    QueuedAtUtc = operation.QueuedAtUtc,
                    LastAttemptAtUtc = DateTime.UtcNow,
                    SyncedAtUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync(cancellationToken);

                if (isTable && !operation.Operation.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                {
                    var incoming = operation.Payload.Deserialize(typeof(Table), JsonOptions) as Table
                                   ?? throw new InvalidOperationException("Payload could not be read.");
                    var syncedTable = await FindExistingAsync(typeof(Table), incoming, cancellationToken) as Table;
                    if (syncedTable is not null)
                    {
                        await PlacementUnitProvisioner.EnsureForTableAsync(db, syncedTable, cancellationToken);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }

                results.Add(Success(operation, "Applied."));
            }
            catch (Exception ex)
            {
                results.Add(Failure(operation, ex.GetBaseException().Message));
                db.ChangeTracker.Clear();
            }
        }

        return Ok(new AdminSyncBatchResponse(results));
    }

    private async Task UpsertAsync(Type entityType, JsonElement payload, CancellationToken cancellationToken)
    {
        var incoming = payload.Deserialize(entityType, JsonOptions)
                       ?? throw new InvalidOperationException("Payload could not be read.");
        var existing = await FindExistingAsync(entityType, incoming, cancellationToken);
        if (existing is null)
        {
            SetIntProperty(incoming, "Id", 0);
            db.Add(incoming);
            return;
        }

        CopyWritableProperties(incoming, existing);
        db.Update(existing);
    }

    private async Task DeleteAsync(Type entityType, JsonElement payload, CancellationToken cancellationToken)
    {
        var incoming = payload.Deserialize(entityType, JsonOptions)
                       ?? throw new InvalidOperationException("Payload could not be read.");
        var existing = await FindExistingAsync(entityType, incoming, cancellationToken);
        if (existing is not null)
            db.Remove(existing);
    }

    private async Task<object?> FindExistingAsync(Type entityType, object incoming, CancellationToken cancellationToken)
    {
        var id = GetIntProperty(incoming, "Id");
        if (id > 0)
        {
            var byId = await db.FindAsync(entityType, [id], cancellationToken);
            if (byId is not null)
                return byId;
        }

        var uniqueId = GetStringProperty(incoming, "UniqueId");
        if (!string.IsNullOrWhiteSpace(uniqueId))
            return await FindByStringPropertyAsync(entityType, "UniqueId", uniqueId, cancellationToken);

        var key = GetStringProperty(incoming, "Key");
        if (!string.IsNullOrWhiteSpace(key))
            return await FindByStringPropertyAsync(entityType, "Key", key, cancellationToken);

        return null;
    }

    private async Task<object?> FindByStringPropertyAsync(
        Type entityType,
        string propertyName,
        string value,
        CancellationToken cancellationToken)
    {
        var method = typeof(AdminSyncController)
            .GetMethod(nameof(FindByStringPropertyGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        return await (Task<object?>)method.Invoke(null, [db, propertyName, value, cancellationToken])!;
    }

    private static async Task<object?> FindByStringPropertyGeneric<TEntity>(
        AppDbContext db,
        string propertyName,
        string value,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return await db.Set<TEntity>().FirstOrDefaultAsync(
            e => EF.Property<string>(e, propertyName) == value,
            cancellationToken);
    }

    private static void CopyWritableProperties(object source, object target)
    {
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.Name is "Id" or "RestaurantId")
                continue;
            if (property.GetIndexParameters().Length > 0)
                continue;
            if (!IsSimpleProperty(property.PropertyType))
                continue;

            property.SetValue(target, property.GetValue(source));
        }
    }

    private static bool IsSimpleProperty(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(Guid)
               // PublicMenuAsset.Content (product photos, logo bytes) must overwrite on upsert.
               || type == typeof(byte[]);
    }

    private static int GetIntProperty(object instance, string name) =>
        instance.GetType().GetProperty(name)?.GetValue(instance) is int value ? value : 0;

    private static void SetIntProperty(object instance, string name, int value) =>
        instance.GetType().GetProperty(name)?.SetValue(instance, value);

    private static string? GetStringProperty(object instance, string name) =>
        instance.GetType().GetProperty(name)?.GetValue(instance) as string;

    private static AdminSyncOperationResultDto Success(AdminSyncOperationDto operation, string message) =>
        new(operation.IdempotencyKey, operation.EntityName, operation.Operation, true, message);

    private static AdminSyncOperationResultDto Failure(AdminSyncOperationDto operation, string message) =>
        new(operation.IdempotencyKey, operation.EntityName, operation.Operation, false, message);
}
