-- Existing translation approvals were never confirmed by a speaker of the language, so they load as not checked.
ALTER TABLE story_approvals ADD COLUMN speaker_checked boolean NOT NULL DEFAULT false;
UPDATE story_approvals a SET speaker_checked = true FROM stories s WHERE s.id = a.story_id AND a.language = s.original_language;
