using System.Collections.Concurrent;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using OpenBudget.Api.Mcp;

namespace OpenBudget.Api.Realtime;

/// <summary>
/// Lightweight change notification. Contains no financial details: only the budget, the area that changed, the affected
/// Plan month (when one) and the new optimistic version, so open sessions know what to re-fetch.
/// </summary>
public sealed record BudgetChangeEvent(string BudgetId, string Kind, string? Month, long? Version, DateTimeOffset At);

public static class BudgetChangeKinds
{
    public const string Plan = "plan";
    public const string Ledger = "ledger";
    public const string Targets = "targets";
    public const string Planning = "planning";
    public const string Admin = "admin";
}

/// <summary>
/// In-process publish/subscribe hub. Known limitation: subscribers only receive changes committed by the same API
/// process; a multi-instance deployment needs a backplane (for example Azure SignalR Service or Azure Cache for Redis
/// pub/sub) behind this interface.
/// </summary>
public sealed class BudgetChangePublisher
{
    public const int MaxSubscriptionsPerUser = 10;
    private readonly ConcurrentDictionary<Guid, Subscription> subscriptions = new();

    public Subscription? Subscribe(string budgetId, string userId)
    {
        if (subscriptions.Values.Count(x => x.UserId == userId) >= MaxSubscriptionsPerUser) return null;
        var subscription = new Subscription(this, budgetId, userId);
        subscriptions[subscription.Id] = subscription;
        return subscription;
    }

    public int SubscriberCount(string budgetId) => subscriptions.Values.Count(x => x.BudgetId == budgetId);

    public void Publish(IEnumerable<BudgetChangeEvent> changes)
    {
        foreach (var change in changes)
            foreach (var subscription in subscriptions.Values.Where(x => x.BudgetId == change.BudgetId))
                subscription.Channel.Writer.TryWrite(change);
    }

    public sealed class Subscription : IDisposable
    {
        private readonly BudgetChangePublisher owner;
        internal Subscription(BudgetChangePublisher owner, string budgetId, string userId) { this.owner = owner; BudgetId = budgetId; UserId = userId; }
        public Guid Id { get; } = Guid.NewGuid();
        public string BudgetId { get; }
        public string UserId { get; }
        public Channel<BudgetChangeEvent> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<BudgetChangeEvent>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        public void Dispose() { owner.subscriptions.TryRemove(Id, out _); Channel.Writer.TryComplete(); }
    }
}

/// <summary>
/// Collects which budgets/areas a SaveChanges touched and publishes them only after the surrounding database transaction
/// commits (or immediately after an auto-committed SaveChanges). Rolled-back work is never published.
/// </summary>
public sealed class BudgetChangeInterceptor(BudgetChangePublisher publisher, TimeProvider clock) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private const int MaxPlanMonthsPerCommit = 24;
    private readonly ConditionalWeakTable<DbTransaction, List<BudgetChangeEvent>> pendingByTransaction = new();
    private readonly ConditionalWeakTable<DbContext, List<BudgetChangeEvent>> pendingSave = new();

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result) { Complete(eventData.Context); return result; }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData) { if (eventData.Context is not null) pendingSave.Remove(eventData.Context); }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => PublishTransaction(transaction);

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        PublishTransaction(transaction);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => pendingByTransaction.Remove(transaction);

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        pendingByTransaction.Remove(transaction);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is not BudgetDbContext) return;
        var now = clock.GetUtcNow();
        var events = new List<BudgetChangeEvent>();
        foreach (var entry in context.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var change = Describe(entry.Entity, now);
            if (change is not null) events.Add(change);
        }
        if (events.Count == 0) return;
        pendingSave.AddOrUpdate(context, Collapse(events));
    }

    private void Complete(DbContext? context)
    {
        if (context is null || !pendingSave.TryGetValue(context, out var events)) return;
        pendingSave.Remove(context);
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction is null) { publisher.Publish(events); return; }
        var pending = pendingByTransaction.GetOrCreateValue(transaction);
        lock (pending) { pending.AddRange(events); }
    }

    private void PublishTransaction(DbTransaction transaction)
    {
        if (!pendingByTransaction.TryGetValue(transaction, out var pending)) return;
        pendingByTransaction.Remove(transaction);
        List<BudgetChangeEvent> events;
        lock (pending) { events = Collapse(pending); }
        publisher.Publish(events);
    }

    private static BudgetChangeEvent? Describe(object entity, DateTimeOffset now) => entity switch
    {
        BudgetEntity month => new(month.BudgetId, BudgetChangeKinds.Plan, month.Month, month.Version, now),
        CategoryGroupEntity group => new(group.BudgetId, BudgetChangeKinds.Plan, group.BudgetMonth, null, now),
        CategoryEntity category => new(category.BudgetId, BudgetChangeKinds.Plan, category.BudgetMonth, null, now),
        CategoryTargetEntity target => new(target.BudgetId, BudgetChangeKinds.Targets, null, null, now),
        LedgerStateEntity state => new(state.BudgetId, BudgetChangeKinds.Ledger, null, state.Version, now),
        AccountEntity account => new(account.BudgetId, BudgetChangeKinds.Ledger, null, null, now),
        TransactionEntity transaction => new(transaction.BudgetId, BudgetChangeKinds.Ledger, null, null, now),
        PayeeEntity payee => new(payee.BudgetId, BudgetChangeKinds.Ledger, null, null, now),
        ScheduleEntity schedule => new(schedule.BudgetId, BudgetChangeKinds.Ledger, null, null, now),
        FlagDefinitionEntity flag => new(flag.BudgetId, BudgetChangeKinds.Ledger, null, null, now),
        CreditPayoffTargetEntity payoff => new(payoff.BudgetId, BudgetChangeKinds.Ledger, null, null, now),
        LongTermPlanningStateEntity planning => new(planning.BudgetId, BudgetChangeKinds.Planning, null, planning.Version, now),
        LongTermPlanningCategoryEntity planning => new(planning.BudgetId, BudgetChangeKinds.Planning, null, null, now),
        LongTermPlanningIncomeEntity planning => new(planning.BudgetId, BudgetChangeKinds.Planning, null, null, now),
        BudgetPlanEntity plan => new(plan.Id, BudgetChangeKinds.Admin, null, plan.Version, now),
        BudgetMemberEntity member => new(member.BudgetId, BudgetChangeKinds.Admin, null, null, now),
        BudgetInvitationEntity invitation => new(invitation.BudgetId, BudgetChangeKinds.Admin, null, null, now),
        _ => null
    };

    /// <summary>One event per (budget, kind, month); keeps the highest known version. Large recalculations collapse to month=null.</summary>
    public static List<BudgetChangeEvent> Collapse(IEnumerable<BudgetChangeEvent> events)
    {
        var result = events.GroupBy(x => (x.BudgetId, x.Kind, x.Month))
            .Select(g => g.First() with { Version = g.Max(x => x.Version), At = g.Max(x => x.At) }).ToList();
        foreach (var budget in result.Where(x => x.Kind == BudgetChangeKinds.Plan).GroupBy(x => x.BudgetId).ToList())
        {
            if (budget.Count() <= MaxPlanMonthsPerCommit) continue;
            result.RemoveAll(x => x.BudgetId == budget.Key && x.Kind == BudgetChangeKinds.Plan);
            result.Add(new BudgetChangeEvent(budget.Key, BudgetChangeKinds.Plan, null, null, budget.Max(x => x.At)));
        }
        return result;
    }
}

public static class BudgetEventsEndpoint
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(25);

    /// <summary>
    /// <c>GET /api/budgets/{id}/events</c>: cookie-authenticated server-sent events for owners and editors. Access is
    /// re-checked on every heartbeat and before forwarding membership/budget changes; the stream ends when it is lost.
    /// </summary>
    public static void Map(IEndpointRouteBuilder app) => app.MapGet("/api/budgets/{id}/events", async (string id, HttpContext context,
        ClaimsPrincipal principal, BudgetChangePublisher publisher, IServiceScopeFactory scopes, CancellationToken ct) =>
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();
        if (!McpBudgetIds.IsValid(id) || !await CanAccessAsync(scopes, userId, id, ct)) return Results.NotFound();
        using var subscription = publisher.Subscribe(id, userId);
        if (subscription is null) return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        await context.Response.WriteAsync("retry: 5000\n: connected\n\n", ct);
        await context.Response.Body.FlushAsync(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
                wait.CancelAfter(Heartbeat);
                BudgetChangeEvent? change = null;
                try { change = await subscription.Channel.Reader.ReadAsync(wait.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                if ((change is null || change.Kind == BudgetChangeKinds.Admin) && !await CanAccessAsync(scopes, userId, id, ct)) break;
                if (change is null) await context.Response.WriteAsync(": heartbeat\n\n", ct);
                else await context.Response.WriteAsync($"event: change\ndata: {JsonSerializer.Serialize(change, JsonSerializerOptions.Web)}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ChannelClosedException) { }
        return Results.Empty;
    }).RequireAuthorization();

    private static async Task<bool> CanAccessAsync(IServiceScopeFactory scopes, string userId, string budgetId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<OpenBudgetUser>>();
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.IsSuspended) return false;
        return await scope.ServiceProvider.GetRequiredService<BudgetSharingService>().CanAccessBudgetAsync(userId, budgetId, ct);
    }
}
