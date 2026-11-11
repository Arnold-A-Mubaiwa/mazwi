namespace Mazwi.Domain;

public sealed class AccountExistsException() : Exception("An account with this email address already exists. Sign in instead.");
