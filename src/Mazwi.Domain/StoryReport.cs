namespace Mazwi.Domain;

public enum ReportReason
{
    NoPermission,
    Harmful,
    Misleading,
    Other,
}

// A viewer's report about a published story. Nothing about who reported it is kept.
public sealed class StoryReport
{
    public const int MaximumNoteLength = 500;
    public Guid Id { get; }
    public Guid StoryId { get; }
    public ReportReason Reason { get; }
    public string? Note { get; }
    public DateTimeOffset CreatedAt { get; }

    private StoryReport(Guid id, Guid storyId, ReportReason reason, string? note, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || storyId == Guid.Empty || !Enum.IsDefined(reason) || createdAt == default)
        {
            throw new DomainException("A report needs a story, a reason and a time.");
        }

        string? text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is not null && (text.Length > MaximumNoteLength || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r'))))
        {
            throw new DomainException("Keep the note to 500 characters of plain text.");
        }

        Id = id;
        StoryId = storyId;
        Reason = reason;
        Note = text;
        CreatedAt = createdAt;
    }

    public static StoryReport File(Story story, ReportReason reason, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (!story.CanBeReported)
        {
            throw new DomainException("Only stories in the feed can be reported.");
        }

        return new StoryReport(Guid.NewGuid(), story.Id, reason, note, now);
    }
}

// What a moderator sees for one reported story.
public sealed record ReportedStory(Guid StoryId, string Title, string ArtistName, bool HiddenForReview, int OpenReports,
    IReadOnlyList<ReportReason> Reasons, IReadOnlyList<string> Notes, DateTimeOffset LatestReportAt);
