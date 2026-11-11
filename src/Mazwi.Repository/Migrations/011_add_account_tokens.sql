-- Email verification and a security stamp that changes with the password, ending older sessions.
ALTER TABLE artists ADD COLUMN email_verified_at timestamptz NULL;
ALTER TABLE artists ADD COLUMN security_stamp uuid NOT NULL DEFAULT gen_random_uuid();

-- Single-use emailed links. Only SHA-256 hashes of the secrets are stored.
CREATE TABLE account_tokens (
    id uuid PRIMARY KEY,
    artist_id uuid NOT NULL REFERENCES artists (id) ON DELETE CASCADE,
    purpose varchar(20) NOT NULL CHECK (purpose IN ('PasswordReset', 'EmailVerification')),
    token_hash char(64) NOT NULL UNIQUE CHECK (token_hash ~ '^[0-9a-f]{64}$'),
    expires_at timestamptz NOT NULL,
    used_at timestamptz NULL
);
CREATE INDEX account_tokens_artist_purpose_idx ON account_tokens (artist_id, purpose);
