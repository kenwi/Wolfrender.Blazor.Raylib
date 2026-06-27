namespace Game.Features.Recording;

public sealed class RecordingUploadRequest
{
    public required string Name { get; init; }
    public required RecFile Recording { get; init; }
}
