CREATE TABLE stories (
    id uuid PRIMARY KEY,
    title varchar(200) NOT NULL CHECK (length(trim(title)) > 0),
    artist_name varchar(100) NOT NULL CHECK (length(trim(artist_name)) > 0),
    community varchar(100) NOT NULL CHECK (length(trim(community)) > 0),
    country varchar(2) NOT NULL CHECK (country ~ '^[A-Z]{2}$'),
    story_type varchar(20) NOT NULL CHECK (story_type IN ('story', 'poem', 'folktale', 'song')),
    original_language varchar(2) NOT NULL CHECK (original_language IN ('sn', 'xh', 'sw', 'en')),
    recording_extension varchar(5) NOT NULL CHECK (recording_extension IN ('.mp3', '.wav', '.m4a', '.mp4', '.webm')),
    recording_bytes bigint NOT NULL CHECK (recording_bytes BETWEEN 1 AND 25000000),
    permission_recorded_at timestamptz NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'unpublished' CHECK (status = 'unpublished')
);
