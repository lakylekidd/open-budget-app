using Microsoft.EntityFrameworkCore;

namespace OpenBudget.Api;

public sealed record TransactionSelectionRequest(Guid CommandId, long Version, List<Guid>? Ids, string Action,
    string? Status = null, string? Color = null, string? Memo = null, Guid? AccountId = null,
    string? Repeat = null, bool ConfirmReconciledEdit = false);

public sealed partial class LedgerService
{
    public Task<MutationDto> ApplySelectionAsync(TransactionSelectionRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId, request.Version, "transaction.selection", request, async () =>
        {
            if (request.Ids is null || request.Ids.Count is < 1 or > 100 || request.Ids.Contains(Guid.Empty)
                || request.Ids.Distinct().Count() != request.Ids.Count)
                throw new LedgerException("Choose 1–100 distinct actual transactions.");
            if (request.Action is not ("status" or "flag" or "memo" or "move" or "duplicate" or "repeat" or "delete"))
                throw new LedgerException("Unknown selected transaction action.");
            if (request.Action == "status" && request.Status is not ("cleared" or "uncleared")) throw new LedgerException("Use cleared or uncleared status.");
            if (request.Action == "flag") ValidateFlagColor(request.Color);
            if (request.Action == "memo" && (request.Memo is null || request.Memo.Length > 1000)) throw new LedgerException("Memo must be at most 1000 characters.");
            if (request.Action == "repeat" && (request.Repeat is null or "never")) throw new LedgerException("Choose a repeating frequency.");
            if (request.Action == "move") await OpenAccountAsync(request.AccountId ?? Guid.Empty, ct);

            var rows = await Entries.Where(t => request.Ids.Contains(t.Id) && !t.IsDeleted).ToListAsync(ct);
            if (rows.Count != request.Ids.Count) throw new LedgerException("A selected transaction no longer exists. Reload the register.", 404);
            var transferIds = rows.Where(t => t.TransferId != null).Select(t => t.TransferId!.Value).Distinct().ToList();
            // Pair-changing actions include both legs once, even if both were selected.
            if (request.Action is "delete" or "duplicate" or "memo" or "repeat")
            {
                var paired = await Entries.Where(t => t.TransferId != null && transferIds.Contains(t.TransferId.Value) && !t.IsDeleted).ToListAsync(ct);
                rows = rows.Concat(paired).DistinctBy(t => t.Id).ToList();
                if (transferIds.Any(id => rows.Count(t => t.TransferId == id) != 2)) throw new LedgerException("Transfer pair is incomplete.");
            }
            var before = rows.Select(ToDto).ToList();
            List<Guid> invalidated = [];
            foreach (var row in rows)
            {
                await OpenAccountAsync(row.AccountId, ct);
                if (request.Action is "status" or "delete") EnsureEditable(row);
                if (request.Action is "duplicate" or "move" or "repeat" && row.IsOpening) throw new LedgerException("Opening balances cannot be duplicated, moved or repeated.");
                if (request.Action == "move" && row.TransferId != null) throw new LedgerException("Edit the linked transfer to change its accounts together.");
                if (request.Action is "memo" or "move") invalidated.AddRange(await ConfirmEditableAsync(row, request.ConfirmReconciledEdit, ct));
            }
            List<TransactionEntity> copies = [];
            var copiedTransfers = transferIds.ToDictionary(id => id, _ => Guid.NewGuid());
            foreach (var row in rows)
            {
                switch (request.Action)
                {
                    case "status": row.Status = request.Status!; if (request.Status == "cleared") row.IsApproved = true; break;
                    case "flag": row.FlagColor = request.Color; break;
                    case "memo": row.Memo = request.Memo!.Trim(); break;
                    case "move": row.AccountId = request.AccountId!.Value; break;
                    case "delete": row.IsDeleted = true; break;
                    case "duplicate":
                        var id = Guid.NewGuid();
                        copies.Add(new TransactionEntity { Id = id, AccountId = row.AccountId, Date = row.Date, Amount = row.Amount,
                            PayeeId = row.PayeeId, Payee = row.Payee, Memo = row.Memo, CategoryId = row.CategoryId,
                            Status = "uncleared", FlagColor = row.FlagColor, TransferId = row.TransferId is { } pair ? copiedTransfers[pair] : null,
                            Splits = row.Splits.Select(s => new SplitEntity { TransactionId = id, Position = s.Position, CategoryId = s.CategoryId, Amount = s.Amount }).ToList() });
                        break;
                    case "repeat":
                        if (row.TransferId is null) await CreateNextScheduleAsync(row, request.Repeat!, ct);
                        else if (row.Amount < 0) await CreateNextScheduleAsync(row, request.Repeat!, ct, rows.Single(t => t.TransferId == row.TransferId && t.Id != row.Id).AccountId);
                        break;
                }
            }
            db.Transactions.AddRange(copies);
            return new Change(request.CommandId, before, new { action = request.Action,
                parameters = new { request.Status, request.Color, request.Memo, request.AccountId, request.Repeat },
                entries = rows.Select(ToDto).ToList(), copies = copies.Select(ToDto).ToList(), invalidatedReconciliations = invalidated });
        }, ct);
}
