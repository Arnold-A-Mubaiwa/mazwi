-- Artist accounts. Emails are stored lower-case; passwords only as framework hashes.
CREATE TABLE artists (
    id uuid PRIMARY KEY,
    email varchar(254) NOT NULL UNIQUE CHECK (email = lower(email) AND position('@' IN email) > 1),
    password_hash varchar(1000) NOT NULL CHECK (length(password_hash) > 0),
    created_at timestamptz NOT NULL
);

-- Stories saved before accounts keep a null owner until the owner claims them with --claim-stories.
ALTER TABLE stories ADD COLUMN owner_id uuid NULL REFERENCES artists (id) ON DELETE SET NULL;
CREATE INDEX stories_owner_recent_idx ON stories (owner_id, permission_recorded_at DESC, id DESC);
