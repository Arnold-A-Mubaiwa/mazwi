# Artist-timed subtitles

Issue #34 lets the artist time subtitles while listening, so viewers can follow each line as it is spoken. No AI is involved.

## Timing a language

1. In the story editor, save the text first. Each non-empty line becomes one subtitle; blank lines are ignored.
2. Open **Time … subtitles** under the original or a translation.
3. Play the recording and press **Mark next line** as each line begins, then once more when the words end. You can also type the seconds (for example `1.5`).
4. Press **Save … timing**, then approve the language again.

Which recording to time against:
- The original is timed against the original recording.
- A translation with its own recording (uploaded or AI voice) is timed against that recording.
- A translation without one is timed against the original recording, because that is what viewers hear with it.

## Rules

- Every line needs a start time. Each start must be later than the one before, and the end must come after the last start, within four hours. A line ends where the next one starts.
- Timing is part of what the artist approves. Saving the original's timing removes every approval, just as a transcript edit does; saving a translation's timing removes only that language's approval.
- Timing is deleted when it can no longer fit: when that language's text changes, or its recording is replaced (uploaded or generated). Changing only translation notes keeps it.

## Viewing

- The viewer shows the current line under the player for audio and video.
- Video players also get an off-by-default WebVTT track ("Artist-timed subtitles") for full-screen viewing.
- Cues always belong to the recording being played. When a viewer switches a translated recording to **Hear original**, the translation's timing does not fit, so no line is shown.

| Endpoint | Purpose |
| --- | --- |
| `PUT /api/stories/{id}/subtitles/{language}` | Owner only. `{ startsMs: number[], endMs, reviewVersion }`; 400 if the times or line count are wrong, 409 if the story changed |
| `GET /api/feed/{id}/subtitles?language=&version=&originalAudio=` | WebVTT for the approved version the recording route plays; 404 if untimed or unapproved |

The feed response also carries `cues` and `subtitlesUrl` when the played version is timed. Migration 009 adds `story_subtitle_timings`.

## Tests

- **Domain:** timing rules and boundaries, line splitting, which changes revoke approval or delete timing, and choosing cues per played version.
- **Application:** saving, version conflicts and feed cues.
- **Repository (real PostgreSQL):** round-trips, revocation, stale versions and clean-up on changes.
- **Endpoints:** status codes, WebVTT format and escaping, and a real database round-trip.
- **Browser:** `e2e/subtitles.spec.ts` times two lines in the editor, approves, and checks the viewer shows the right line at 0.1 s and 0.5 s and serves the matching WebVTT.

## Limits

- Timing is one start per line. Very long lines are not split.
- Times typed by hand are not checked against the recording's length.
- There are no AI-generated timestamps: OpenAI timestamp support for `gpt-transcribe` was not verified for these languages.
