namespace Mazwi.Domain;

public sealed record ApprovedStoryVersion(string TextLanguage, string AudioLanguage, StoryText Text, Guid MediaId, StoryRecording Recording);
