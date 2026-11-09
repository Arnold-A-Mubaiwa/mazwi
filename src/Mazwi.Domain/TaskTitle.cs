namespace Mazwi.Domain;

public sealed record TaskTitle
{
    public const int MaximumLength = 200;
    public string Value { get; }

    public TaskTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaximumLength)
        {
            throw new DomainException("A task title must contain 1 to 200 non-whitespace characters.");
        }

        Value = value.Trim();
    }
}
