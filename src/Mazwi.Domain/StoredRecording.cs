namespace Mazwi.Domain;

// One recording file kept for a story: the original or a translated recording.
public sealed record StoredRecording(Guid MediaId, StoryRecording File);
