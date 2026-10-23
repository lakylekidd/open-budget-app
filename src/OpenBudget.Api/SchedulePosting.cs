using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
namespace OpenBudget.Api;
public sealed partial class LedgerService
{
    public async Task<int> PostDueSchedulesAsync(CancellationToken ct)
    {
        var today=DateOnly.FromDateTime(clock.GetLocalNow().DateTime);var blocked=new List<Guid>();var posted=0;
        while(posted<32)
        {
            db.ChangeTracker.Clear();
            var rule=await db.Schedules.AsNoTracking().Where(r=>!r.IsEnded&&!r.IsPaused&&r.NextDate<=today&&!blocked.Contains(r.Id)).OrderBy(r=>r.NextDate).ThenBy(r=>r.Id).FirstOrDefaultAsync(ct);
            if(rule is null)break;
            var occurrenceId=RecurrenceRules.OccurrenceId(rule.Id,rule.Sequence);
            var pending=await db.ScheduleOccurrences.AsNoTracking().SingleOrDefaultAsync(o=>o.Id==occurrenceId,ct);
            if(pending?.DeferredDate>today){blocked.Add(rule.Id);continue;}
            var version=await db.LedgerStates.AsNoTracking().Select(s=>s.Version).SingleAsync(ct);
            var commandId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"due/{occurrenceId:D}"))[..16]);
            try { await ScheduleActionAsync(rule.Id,new(commandId,version,"post",occurrenceId,AutomaticallyPosted:true),ct);posted++; }
            catch(LedgerException error)
            {
                db.ChangeTracker.Clear();blocked.Add(rule.Id);
                if(error.StatusCode!=409 && error.StatusCode!=404)
                    await gate.RunAsync(async()=>{var current=await db.Schedules.SingleAsync(r=>r.Id==rule.Id,ct);if(current.Sequence==rule.Sequence&&!current.IsEnded&&!current.IsPaused&&(await db.LedgerStates.SingleAsync(ct)).Version==version){current.PostingError=error.Message;await db.SaveChangesAsync(ct);}return true;},ct);
            }
        }
        db.ChangeTracker.Clear();return posted;
    }
    public Task<MutationDto> ReviewTransactionAsync(Guid id, ReviewTransactionRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId,request.Version,$"transaction.{request.Action}/{id}",request,async()=>{
            if(request.Action is not ("approve" or "reject"))throw new LedgerException("Choose approve or reject.");
            var entry=await FindEntryAsync(id,ct);
            if(entry.IsApproved)throw new LedgerException("This transaction was already reviewed. Reload the register.",409);
            await OpenAccountAsync(entry.AccountId,ct);
            var rows=request.Action=="reject"&&entry.TransferId is not null?await Entries.Where(t=>t.TransferId==entry.TransferId&&!t.IsDeleted).ToListAsync(ct):[entry];
            var before=rows.Select(ToDto).ToList();
            foreach(var row in rows){if(request.Action=="approve")row.IsApproved=true;else{EnsureEditable(row);await OpenAccountAsync(row.AccountId,ct);row.IsDeleted=true;}}
            return new Change(entry.TransferId??id,before,rows.Select(ToDto).ToList());
        },ct);
}
public sealed class SchedulePostingService(IServiceScopeFactory scopes, ILogger<SchedulePostingService> logger)
{
    public async Task ProcessAllBudgetsAsync(CancellationToken cancellationToken)
    {
        await using var listingScope = scopes.CreateAsyncScope();
        var budgetIds = await listingScope.ServiceProvider.GetRequiredService<BudgetDbContext>().BudgetPlans
            .AsNoTracking().Where(plan => !plan.IsArchived).Select(plan => plan.Id).ToListAsync(cancellationToken);
        foreach (var budgetId in budgetIds)
        {
            try
            {
                await using var budgetScope = scopes.CreateAsyncScope();
                budgetScope.ServiceProvider.GetRequiredService<BudgetRequestContext>().BudgetId = budgetId;
                await budgetScope.ServiceProvider.GetRequiredService<LedgerService>().PostDueSchedulesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { logger.LogWarning("Scheduled posting could not complete for one budget; it will retry on the next pass."); }
        }
    }
}

public sealed class SchedulePostingWorker(IServiceScopeFactory scopes, ILogger<SchedulePostingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SchedulePostingService>().ProcessAllBudgetsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Scheduled posting could not enumerate budgets; it will retry on the next pass."); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
