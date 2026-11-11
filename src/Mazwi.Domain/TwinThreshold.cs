namespace Mazwi.Domain;

/// <summary>Minimum similarity for a suggestion. Uncalibrated: tune it with real stories.</summary>
public sealed record TwinThreshold
{
    public const double DefaultValue = 0.8;

    public double Value { get; }

    public TwinThreshold(double value)
    {
        if (double.IsNaN(value) || value is <= 0 or > 1)
        {
            throw new DomainException("The twin threshold must be above 0 and at most 1.");
        }

        Value = value;
    }
}
