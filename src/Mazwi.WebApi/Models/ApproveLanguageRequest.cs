namespace Mazwi.WebApi.Models;

// SpeakerChecked is optional so older clients keep working; leaving it out means "not checked by a speaker".
public sealed record ApproveLanguageRequest(Guid ReviewVersion, bool SpeakerChecked = false);
