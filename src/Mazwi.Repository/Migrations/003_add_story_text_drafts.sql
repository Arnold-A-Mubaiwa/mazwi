ALTER TABLE stories ADD COLUMN transcript text
    CHECK (transcript IS NULL OR length(trim(transcript)) BETWEEN 1 AND 20000);

CREATE TABLE story_translation_drafts (
    story_id uuid NOT NULL REFERENCES stories(id),
    language varchar(2) NOT NULL CHECK (language IN ('sn', 'xh', 'sw', 'en')),
    text text NOT NULL CHECK (length(trim(text)) BETWEEN 1 AND 20000),
    PRIMARY KEY (story_id, language)
);
