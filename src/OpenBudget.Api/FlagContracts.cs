namespace OpenBudget.Api;
public sealed record FlagDto(string Color, string Name);
public sealed record FlagPaletteDto(long Version, List<FlagDto> Flags);
public sealed record RenameFlagsRequest(Guid CommandId, long Version, List<FlagDto> Flags);
public sealed record SetFlagRequest(Guid CommandId, long Version, string? Color);
