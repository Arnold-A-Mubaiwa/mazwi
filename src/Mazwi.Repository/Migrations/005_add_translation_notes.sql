ALTER TABLE story_translation_drafts ADD COLUMN notes text[] NOT NULL DEFAULT ARRAY[]::text[];
ALTER TABLE story_translation_drafts ADD CONSTRAINT valid_translation_notes_count CHECK (cardinality(notes) <= 20);
