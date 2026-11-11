using System.Globalization;
using System.Text;
using Mazwi.Domain;

namespace Mazwi.Repository;

// Development email: each message is written as an .eml file instead of being sent. Open it with any mail program.
public sealed class LocalOutboxEmailSender(string root) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Directory.CreateDirectory(root);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var content = new StringBuilder()
            .Append("From: Mazwi <no-reply@mazwi.local>\r\n")
            .Append("To: ").Append(message.To.Value).Append("\r\n")
            .Append("Subject: ").Append(message.Subject).Append("\r\n")
            .Append("Date: ").Append(now.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("Content-Type: text/plain; charset=utf-8\r\n\r\n")
            .Append(message.Body.ReplaceLineEndings("\r\n")).Append("\r\n")
            .ToString();
        string name = $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml";
        await File.WriteAllTextAsync(Path.Combine(root, name), content, new UTF8Encoding(false), cancellationToken);
    }
}
