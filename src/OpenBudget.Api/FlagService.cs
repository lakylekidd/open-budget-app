using Microsoft.EntityFrameworkCore;
namespace OpenBudget.Api;
public sealed partial class LedgerService
{
    public static readonly string[] FlagColors = ["red","orange","yellow","green","blue","purple"];
    public static void ValidateFlagColor(string? color) { if(color is not null && !FlagColors.Contains(color)) throw new LedgerException("Choose one of the six flag colors or clear the flag."); }
    public async Task<FlagPaletteDto> GetFlagsAsync(CancellationToken ct) => new((await db.LedgerStates.SingleAsync(ct)).Version,
        (await db.FlagDefinitions.ToListAsync(ct)).OrderBy(f=>Array.IndexOf(FlagColors,f.Color)).Select(f=>new FlagDto(f.Color,f.Name)).ToList());
    public Task<MutationDto> RenameFlagsAsync(RenameFlagsRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId,request.Version,"flags.rename",request,async()=>{
            if(request.Flags is null || request.Flags.Count!=6 || request.Flags.Any(f=>f is null) || request.Flags.Select(f=>f.Color).Distinct().Count()!=6 || request.Flags.Any(f=>!FlagColors.Contains(f.Color)||string.IsNullOrWhiteSpace(f.Name)||f.Name.Trim().Length>40)) throw new LedgerException("Provide all six flag names, each between 1 and 40 characters.");
            var rows=await db.FlagDefinitions.ToListAsync(ct);var before=rows.Select(r=>new FlagDto(r.Color,r.Name)).ToList();
            foreach(var row in rows) row.Name=request.Flags.Single(f=>f.Color==row.Color).Name.Trim();
            return new Change(Guid.Empty,before,rows.Select(r=>new FlagDto(r.Color,r.Name)).ToList());
        },ct);
    public Task<MutationDto> SetFlagAsync(Guid id, bool scheduled, SetFlagRequest request, CancellationToken ct) =>
        ExecuteAsync(request.CommandId,request.Version,$"{(scheduled?"schedule":"transaction")}.flag/{id}",request,async()=>{
            ValidateFlagColor(request.Color);
            if(scheduled){var rule=await FindScheduleAsync(id,ct);var before=rule.FlagColor;rule.FlagColor=request.Color;return new Change(id,new{color=before},new{color=rule.FlagColor});}
            var entry=await FindEntryAsync(id,ct);var old=entry.FlagColor;entry.FlagColor=request.Color;
            return new Change(id,new{color=old},new{color=entry.FlagColor});
        },ct);
}
