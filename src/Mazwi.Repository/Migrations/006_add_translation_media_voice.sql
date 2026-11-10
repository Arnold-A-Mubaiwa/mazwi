ALTER TABLE story_translation_drafts ADD COLUMN media_voice varchar(10);
ALTER TABLE story_translation_drafts ADD CONSTRAINT valid_translation_media_voice CHECK (
    media_voice IS NULL OR (media_id IS NOT NULL AND media_voice IN ('standard', 'clone')));
