namespace Mazwi.Domain;

public sealed record PasswordHash
{
    public string Value { get; }

    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1000)
        {
            throw new DomainException("A password hash is required.");
        }

        Value = value;
    }

    public override string ToString() => "PasswordHash";
}
