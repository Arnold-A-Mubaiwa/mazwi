namespace Mazwi.Domain;

public interface IPasswordHasher
{
    PasswordHash Hash(NewPassword password);
    bool Verify(PasswordHash hash, string attempt);
}
