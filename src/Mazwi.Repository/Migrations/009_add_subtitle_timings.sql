-- When each line of a language's saved text starts, in milliseconds, set by the artist while listening.
CREATE TABLE story_subtitle_timings (
    story_id uuid NOT NULL REFERENCES stories (id) ON DELETE CASCADE,
    language varchar(2) NOT NULL CHECK (language IN ('sn', 'xh', 'sw', 'en')),
    starts_ms integer[] NOT NULL CHECK (cardinality(starts_ms) BETWEEN 1 AND 2000),
    end_ms integer NOT NULL CHECK (end_ms > 0 AND end_ms <= 14400000),
    PRIMARY KEY (story_id, language)
);
