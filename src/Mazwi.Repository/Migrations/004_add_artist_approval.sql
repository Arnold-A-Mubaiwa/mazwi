ALTER TABLE stories DROP COLUMN status;
ALTER TABLE stories ADD COLUMN review_version uuid NOT NULL DEFAULT gen_random_uuid();
ALTER TABLE story_translation_drafts ADD COLUMN media_id uuid;
ALTER TABLE story_translation_drafts ADD COLUMN media_extension varchar(5);
ALTER TABLE story_translation_drafts ADD COLUMN media_bytes bigint;
ALTER TABLE story_translation_drafts ADD CONSTRAINT valid_translation_media CHECK (
    (media_id IS NULL AND media_extension IS NULL AND media_bytes IS NULL) OR
    (media_id IS NOT NULL AND media_extension IS NOT NULL AND media_bytes IS NOT NULL
        AND media_extension IN ('.mp3', '.wav', '.m4a', '.mp4', '.webm') AND media_bytes BETWEEN 1 AND 25000000));
ALTER TABLE story_translation_drafts DROP CONSTRAINT story_translation_drafts_story_id_fkey;
ALTER TABLE story_translation_drafts ADD FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE CASCADE;
CREATE TABLE story_approvals (
    story_id uuid NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
    language varchar(2) NOT NULL CHECK (language IN ('sn', 'xh', 'sw', 'en')),
    approved_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (story_id, language)
);
