namespace Mazwi.Domain;

// A password chosen at sign-up. It is hashed straight away and never stored or logged.
public sealed class NewPassword
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 128;
    public string Value { get; }

    public NewPassword(string value)
    {
        if (value is null || value.Length is < MinimumLength or > MaximumLength || string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Choose a password of 10–128 characters.");
        }

        Value = value;
    }

    public override string ToString() => "********";
}
