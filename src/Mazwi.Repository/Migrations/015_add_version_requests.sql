-- Anonymous "tell this story in my language" requests. Only counts per story and language; nothing about listeners.
CREATE TABLE story_version_requests (
    story_id uuid NOT NULL REFERENCES stories (id) ON DELETE CASCADE,
    language varchar(2) NOT NULL CHECK (language ~ '^[a-z]{2}$'),
    request_count integer NOT NULL CHECK (request_count > 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (story_id, language)
);
