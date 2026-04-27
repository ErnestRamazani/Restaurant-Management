using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// PostgreSQL with <c>EnableRetryOnFailure</c> requires user transactions to run inside
/// <see cref="DatabaseFacade.CreateExecutionStrategy"/>.
/// </summary>
public static class DatabaseResilientTransaction
{
    public static T Execute<T>(AppDbContext db, Func<T> operation) =>
        db.Database.CreateExecutionStrategy().Execute(operation);

    public static void Execute(AppDbContext db, Action operation) =>
        db.Database.CreateExecutionStrategy().Execute(operation);
}
