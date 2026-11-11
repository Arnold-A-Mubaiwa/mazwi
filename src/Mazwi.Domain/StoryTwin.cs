namespace Mazwi.Domain;

public enum TwinStatus
{
    Suggested,
    Confirmed,
    Rejected,
}

/// <summary>
/// Two stories by different artists, in different languages, that may be the same tale.
/// The pair is stored once, in a fixed order, so a rejected pair can never be suggested again.
/// </summary>
public sealed class StoryTwin
{
    public Guid StoryA { get; }
    public Guid StoryB { get; }
    public double Similarity { get; }
    public TwinStatus Status { get; private set; }

    private StoryTwin(Guid storyA, Guid storyB, double similarity, TwinStatus status)
    {
        if (storyA == Guid.Empty || storyB == Guid.Empty || storyA == storyB)
        {
            throw new DomainException("A twin needs two different stories.");
        }

        if (Order(storyA, storyB) >= 0)
        {
            throw new DomainException("Twin stories must be stored in order.");
        }

        if (double.IsNaN(similarity) || similarity is < -1 or > 1)
        {
            throw new DomainException("Similarity must be between -1 and 1.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new DomainException("Unknown twin status.");
        }

        StoryA = storyA;
        StoryB = storyB;
        Similarity = similarity;
        Status = status;
    }

    public static StoryTwin Suggest(Guid story, Guid other, double similarity)
    {
        if (story == other)
        {
            throw new DomainException("A twin needs two different stories.");
        }

        (Guid a, Guid b) = Pair(story, other);
        return new StoryTwin(a, b, similarity, TwinStatus.Suggested);
    }

    public static StoryTwin Rehydrate(Guid storyA, Guid storyB, double similarity, TwinStatus status) => new(storyA, storyB, similarity, status);

    /// <summary>The fixed order of a pair; PostgreSQL orders uuids the same way (by their text form).</summary>
    public static (Guid A, Guid B) Pair(Guid story, Guid other) => Order(story, other) < 0 ? (story, other) : (other, story);

    public bool Involves(Guid story) => story == StoryA || story == StoryB;

    public Guid TwinOf(Guid story) => story == StoryA ? StoryB
        : story == StoryB ? StoryA
        : throw new DomainException("That story is not part of this twin.");

    /// <summary>Either artist may confirm a suggestion. A rejected pair stays rejected.</summary>
    public void Confirm()
    {
        if (Status == TwinStatus.Rejected)
        {
            throw new DomainException("This pair was marked as not the same story.");
        }

        Status = TwinStatus.Confirmed;
    }

    /// <summary>Either artist may reject a suggestion or unlink a confirmed twin. It is final.</summary>
    public void Reject() => Status = TwinStatus.Rejected;

    private static int Order(Guid left, Guid right) => string.CompareOrdinal(left.ToString("D"), right.ToString("D"));
}
