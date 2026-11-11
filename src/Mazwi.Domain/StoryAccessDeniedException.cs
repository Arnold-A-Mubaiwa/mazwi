namespace Mazwi.Domain;

public sealed class StoryAccessDeniedException() : Exception("This story belongs to another artist.");
