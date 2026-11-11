namespace Mazwi.Domain;

public sealed record EmailMessage
{
    public EmailAddress To { get; }
    public string Subject { get; }
    public string Body { get; }

    public EmailMessage(EmailAddress to, string subject, string body)
    {
        ArgumentNullException.ThrowIfNull(to);
        // Line breaks in a subject would let text be injected as extra email headers.
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 200 || subject.Any(char.IsControl) || string.IsNullOrWhiteSpace(body) || body.Length > 20_000)
        {
            throw new DomainException("An email needs a one-line subject and a body.");
        }

        To = to;
        Subject = subject.Trim();
        Body = body;
    }
}
