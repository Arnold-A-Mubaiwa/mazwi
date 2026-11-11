-- Story Twins: artists can opt a story out; Mazwi keeps a private gist and embedding per story, and suggested pairs.
ALTER TABLE stories ADD COLUMN twins_allowed boolean NOT NULL DEFAULT true;

-- Stories that may be matched or shown as twins: published (original approved), owned, not hidden, not opted out.
CREATE VIEW twinnable_stories AS
SELECT s.id, s.owner_id, s.title, s.artist_name, s.community, s.original_language, s.story_type, s.transcript
FROM stories s
WHERE s.owner_id IS NOT NULL AND s.twins_allowed AND NOT s.hidden_for_review AND s.transcript IS NOT NULL
    AND EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = s.id AND a.language = s.original_language);

-- One private fingerprint per story. A row with no gist records a failed attempt (retried later).
CREATE TABLE story_fingerprints (
    story_id uuid PRIMARY KEY REFERENCES stories (id) ON DELETE CASCADE,
    source_hash char(64) NOT NULL CHECK (source_hash ~ '^[0-9a-f]{64}$'),
    gist text NULL CHECK (gist IS NULL OR length(gist) BETWEEN 1 AND 2000),
    vector real[] NULL CHECK (vector IS NULL OR cardinality(vector) BETWEEN 1 AND 4096),
    failed_at timestamptz NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK ((gist IS NULL) = (vector IS NULL)),
    CHECK (gist IS NOT NULL OR failed_at IS NOT NULL)
);

-- Each pair is stored once in a fixed order, so a rejected pair is never suggested again.
CREATE TABLE story_twins (
    story_a uuid NOT NULL REFERENCES stories (id) ON DELETE CASCADE,
    story_b uuid NOT NULL REFERENCES stories (id) ON DELETE CASCADE,
    similarity double precision NOT NULL CHECK (similarity BETWEEN -1 AND 1),
    status text NOT NULL CHECK (status IN ('Suggested', 'Confirmed', 'Rejected')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (story_a, story_b),
    CHECK (story_a < story_b)
);
CREATE INDEX story_twins_story_b_idx ON story_twins (story_b);
