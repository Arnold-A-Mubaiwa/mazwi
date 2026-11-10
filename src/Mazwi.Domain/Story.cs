namespace Mazwi.Domain;

public sealed class Story
{
    public Guid Id { get; }
    public StoryDetails Details { get; }
    public StoryRecording Recording { get; }
    public DateTimeOffset PermissionRecordedAt { get; }
    public string Status => "unpublished";

    private Story(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt)
    {
        if (id == Guid.Empty || permissionRecordedAt == default)
        {
            throw new DomainException("A story requires an identity and recorded permission.");
        }

        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(recording);
        Id = id;
        Details = details;
        Recording = recording;
        PermissionRecordedAt = permissionRecordedAt;
    }

    public static Story Upload(StoryDetails details, StoryRecording recording, bool permission)
    {
        if (!permission)
        {
            throw new DomainException("Permission to store and process this recording is required.");
        }

        return new Story(Guid.NewGuid(), details, recording, DateTimeOffset.UtcNow);
    }

    public static Story Rehydrate(Guid id, StoryDetails details, StoryRecording recording, DateTimeOffset permissionRecordedAt)
        => new(id, details, recording, permissionRecordedAt);
}
