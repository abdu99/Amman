namespace Aman.Shared.Container;

/// <summary>One row of the Encoder's playlist, before it is sealed into a container.</summary>
public sealed class VideoSourceItem
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string FilePath { get; init; }
}
