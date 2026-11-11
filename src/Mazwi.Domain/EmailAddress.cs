namespace Mazwi.Domain;

public sealed record EmailAddress
{
    public const int MaximumLength = 254;
    public string Value { get; }

    // Stored lower-case so one person cannot register the same address twice with different casing.
    public EmailAddress(string value)
    {
        string email = value?.Trim().ToLowerInvariant() ?? string.Empty;
        int at = email.IndexOf('@', StringComparison.Ordinal);
        bool shaped = at > 0 && at == email.LastIndexOf('@') && at < email.Length - 1 && email[(at + 1)..].Contains('.', StringComparison.Ordinal)
            && !email.EndsWith('.') && !email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
        if (email.Length > MaximumLength || !shaped)
        {
            throw new DomainException("Enter a valid email address.");
        }

        Value = email;
    }
}
