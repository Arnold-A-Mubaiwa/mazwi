namespace Mazwi.Application;

/// <summary>A confirmed twin as a viewer sees it, and whether its artist approved a version in the viewer's language.</summary>
public sealed record ViewerTwinDto(Guid StoryId, string Title, string ArtistName, string Community, string Language, bool AvailableInYourLanguage);
