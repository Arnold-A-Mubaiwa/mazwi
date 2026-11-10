namespace Mazwi.WebApi.Models;

public sealed record TranslationDraftResponse(string Language, string Text, string Status, Guid? RecordingId = null, string? RecordingExtension = null);
