namespace Mazwi.Domain;

public sealed class StoryConflictException() : Exception("This story changed. Reload it, check the saved content, then try again.");
