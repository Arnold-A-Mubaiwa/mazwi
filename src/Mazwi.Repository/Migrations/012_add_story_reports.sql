-- Moderators can hide a reported story from the feed until they restore it.
ALTER TABLE stories ADD COLUMN hidden_for_review boolean NOT NULL DEFAULT false;

-- Viewer reports. No reporter identity, address or cookie is stored.
CREATE TABLE story_reports (
    id uuid PRIMARY KEY,
    story_id uuid NOT NULL REFERENCES stories (id) ON DELETE CASCADE,
    reason varchar(20) NOT NULL CHECK (reason IN ('NoPermission', 'Harmful', 'Misleading', 'Other')),
    note varchar(500) NULL,
    created_at timestamptz NOT NULL,
    resolved_at timestamptz NULL
);
CREATE INDEX story_reports_open_idx ON story_reports (story_id) WHERE resolved_at IS NULL;
