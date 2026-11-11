-- Plays per story, approved language and UTC day. No viewer identity, address or cookie is stored.
CREATE TABLE story_play_counts (
    story_id uuid NOT NULL REFERENCES stories (id) ON DELETE CASCADE,
    language varchar(2) NOT NULL CHECK (language IN ('sn', 'xh', 'sw', 'en')),
    day date NOT NULL,
    plays integer NOT NULL CHECK (plays > 0),
    PRIMARY KEY (story_id, language, day)
);
