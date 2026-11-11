using Mazwi.Domain;
using Microsoft.AspNetCore.Identity;

namespace Mazwi.WebApi;

// Adapts the framework's salted PBKDF2 hasher; Identity's EF stores are not used.
public sealed class AspNetPasswordHasher : Mazwi.Domain.IPasswordHasher
{
    private static readonly object User = new();
    private readonly PasswordHasher<object> _hasher = new();

    public PasswordHash Hash(NewPassword password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return new PasswordHash(_hasher.HashPassword(User, password.Value));
    }

    public bool Verify(PasswordHash hash, string attempt)
    {
        ArgumentNullException.ThrowIfNull(hash);
        try
        {
            return _hasher.VerifyHashedPassword(User, hash.Value, attempt) is not PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // A stored hash that is not valid base64 can never match.
            return false;
        }
    }
}
