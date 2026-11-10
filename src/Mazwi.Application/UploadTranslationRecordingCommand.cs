namespace Mazwi.Application;

public sealed record UploadTranslationRecordingCommand(Guid StoryId, string Language, string FileName, long Bytes, bool Permission, Guid ReviewVersion);
