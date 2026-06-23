namespace Transportados.Client.Components.Shared;

public sealed record MobileSortOption(string Value, string Label);

public sealed record MobileSortChangedEventArgs(string SortBy, bool SortDescending);
