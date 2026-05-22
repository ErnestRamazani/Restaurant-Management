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

    public static Task<TResult> ExecuteAsync<TState, TResult>(
        AppDbContext db,
        TState state,
        Func<AppDbContext, TState, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            state,
            (context, s, ct) => operation((AppDbContext)context, s, ct),
            verifySucceeded: null,
            cancellationToken);
}
