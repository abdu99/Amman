namespace Aman.Player.ViewModels;

public sealed class PlaylistEntryViewModel
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required int Index { get; init; }
}
