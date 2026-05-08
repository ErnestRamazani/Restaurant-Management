using System.Reflection;
using System.Text.Json;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/sync")]
[AllowAnonymous]
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
        [nameof(MoneyTransaction)] = typeof(MoneyTransaction)
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
                if (operation.Operation.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                    await DeleteAsync(entityType, operation.Payload, cancellationToken);
                else
                    await UpsertAsync(entityType, operation.Payload, cancellationToken);

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
        if (string.IsNullOrWhiteSpace(uniqueId))
            return null;

        return await FindByUniqueIdAsync(entityType, uniqueId, cancellationToken);
    }

    private async Task<object?> FindByUniqueIdAsync(Type entityType, string uniqueId, CancellationToken cancellationToken)
    {
        var method = typeof(AdminSyncController)
            .GetMethod(nameof(FindByUniqueIdGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        return await (Task<object?>)method.Invoke(null, [db, uniqueId, cancellationToken])!;
    }

    private static async Task<object?> FindByUniqueIdGeneric<TEntity>(
        AppDbContext db,
        string uniqueId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return await db.Set<TEntity>().FirstOrDefaultAsync(
            e => EF.Property<string>(e, "UniqueId") == uniqueId,
            cancellationToken);
    }

    private static void CopyWritableProperties(object source, object target)
    {
        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.Name == "Id")
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
               || type == typeof(Guid);
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
