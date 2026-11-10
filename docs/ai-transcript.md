# AI transcript first draft (Step 4)

The story editor can transcribe the stored original recording and save the result as the original transcript draft. **Check and correct this before approving.** In live tests, Shona transcripts failed human review, so treat the AI text as a starting point that saves typing, never as a finished transcript.

## Enable locally

`FEATURES.aiTranscript` reads the public build-time boolean `NEXT_PUBLIC_MAZWI_AI_TRANSCRIPT`. It is off unless the value is exactly `true`. Set it in the build environment or ignored web/.env.local, then rebuild the frontend. Typing the transcript by hand works with the flag off and without an API key.

The API reads `OPENAI_API_KEY` and `MAZWI_TRANSCRIPTION_MODEL` from server configuration. The model defaults to the language tool's `gpt-transcribe`. Keep the key in ignored root .env or the server environment, never in a NEXT_PUBLIC variable. There are no new packages and no migration.

## Artist flow

1. Upload a recording with permission and open the story editor.
2. Save or discard any pending edits, then press **AI first draft**. The stored original recording is sent to OpenAI. Each click is one paid request.
3. If a transcript is already saved, the editor asks before replacing it.
4. Listen to the recording and correct every line: names, clan and praise names, proverbs and line breaks. Save the corrected transcript.
5. Approve the original through the existing flow only after checking it.

## Rules

- Saving an AI transcript is a normal transcript save. It revokes the original's approval and every translation's approval.
- The client sends the review version it displayed. If the story changed during transcription, the API returns 409 and discards the result. Existing text is unchanged.
- The recording must be stored and at most 25 MB (uploads are already capped at 25,000,000 bytes). Transcripts longer than 20,000 characters are rejected.

## API and failure behaviour

`POST /api/stories/{id}/drafts/transcript/ai-draft`, JSON body `{ "reviewVersion": "<current UUID>" }`, returns the saved story draft. Missing story returns 404, a missing recording file 422, stale version 409, provider/configuration failure or an empty transcript 502, and timeout 504.

The adapter matches tools/language-check: one multipart request to `/v1/audio/transcriptions` with only the documented `file` and `model` fields, because earlier requests with language or prompt hints failed live. Audio-only ISO-BMFF files (brand `M4A`), including recorder files saved as `.mp4`, are uploaded as `recording.m4a` with type `audio/mp4`. There are no retries, a 3-minute timeout and cancellation. Failures save nothing. The key, upstream bodies and raw exceptions are never logged or returned. A charged request cannot be undone by a failure, conflict or cancellation.

## Known limits

OpenAI lists `mp4` as a supported format but does not say video files are accepted; check a video original on the first live run. OpenAI's pages do not mention Shona or isiXhosa. The 3-minute timeout may need raising for large recordings.

Official reference: [speech to text](https://developers.openai.com/api/docs/guides/speech-to-text).
