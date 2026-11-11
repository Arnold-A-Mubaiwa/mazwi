-- Languages now come from the Domain catalogue (StoryLanguages), so the database only checks the code's shape.
ALTER TABLE stories DROP CONSTRAINT stories_original_language_check;
ALTER TABLE stories ADD CONSTRAINT stories_original_language_check CHECK (original_language ~ '^[a-z]{2}$');
ALTER TABLE story_translation_drafts DROP CONSTRAINT story_translation_drafts_language_check;
ALTER TABLE story_translation_drafts ADD CONSTRAINT story_translation_drafts_language_check CHECK (language ~ '^[a-z]{2}$');
ALTER TABLE story_approvals DROP CONSTRAINT story_approvals_language_check;
ALTER TABLE story_approvals ADD CONSTRAINT story_approvals_language_check CHECK (language ~ '^[a-z]{2}$');
ALTER TABLE story_subtitle_timings DROP CONSTRAINT story_subtitle_timings_language_check;
ALTER TABLE story_subtitle_timings ADD CONSTRAINT story_subtitle_timings_language_check CHECK (language ~ '^[a-z]{2}$');
ALTER TABLE story_play_counts DROP CONSTRAINT story_play_counts_language_check;
ALTER TABLE story_play_counts ADD CONSTRAINT story_play_counts_language_check CHECK (language ~ '^[a-z]{2}$');
