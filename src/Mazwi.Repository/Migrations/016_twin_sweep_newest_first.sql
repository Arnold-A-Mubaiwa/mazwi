-- The twin sweep handles the newest stories first, so a new story is not stuck behind a backlog.
CREATE OR REPLACE VIEW twinnable_stories AS
SELECT s.id, s.owner_id, s.title, s.artist_name, s.community, s.original_language, s.story_type, s.transcript, s.permission_recorded_at
FROM stories s
WHERE s.owner_id IS NOT NULL AND s.twins_allowed AND NOT s.hidden_for_review AND s.transcript IS NOT NULL
    AND EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = s.id AND a.language = s.original_language);
