namespace Mazwi.Domain;

// One counted play: which story, in which approved language, on which UTC day. Nothing identifies the viewer.
public sealed record StoryPlay
{
    public Guid StoryId { get; }
    public string Language { get; }
    public DateOnly Day { get; }

    public StoryPlay(Guid storyId, string language, DateOnly day)
    {
        if (storyId == Guid.Empty || !StoryDetails.SupportsLanguage(language) || day == default)
        {
            throw new DomainException("A play needs a story, a supported language and a day.");
        }

        StoryId = storyId;
        Language = language;
        Day = day;
    }
}

// Plays of one approved language of one story, as its owner sees them.
public sealed record StoryPlayTotal
{
    public Guid StoryId { get; }
    public string Language { get; }
    public int Plays { get; }
    public int PlaysLast7Days { get; }

    public StoryPlayTotal(Guid storyId, string language, int plays, int playsLast7Days)
    {
        if (storyId == Guid.Empty || !StoryDetails.SupportsLanguage(language) || plays < 0 || playsLast7Days < 0 || playsLast7Days > plays)
        {
            throw new DomainException("Play totals must be non-negative and the last seven days cannot exceed the total.");
        }

        StoryId = storyId;
        Language = language;
        Plays = plays;
        PlaysLast7Days = playsLast7Days;
    }
}
