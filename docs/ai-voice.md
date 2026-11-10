# AI voice for translations (Step 2)

The story editor can turn a saved translation into a standard AI voice (not the artist's voice) and save it as that language's translated recording. It never approves anything automatically. The artist must listen to the whole recording and approve the language again before viewers hear it.

## Enable locally

`FEATURES.aiVoice` reads the public build-time boolean `NEXT_PUBLIC_MAZWI_AI_VOICE`. It is off unless the value is exactly `true`. Set it in the build environment or ignored web/.env.local, then rebuild the frontend. Uploading a recording by hand works with the flag off and without an API key.

The API reads `OPENAI_API_KEY` and `MAZWI_SPEECH_MODEL` from server configuration. The model defaults to the language tool's `gpt-realtime-2.1-mini`. Keep the key in ignored root .env or the server environment, never in a NEXT_PUBLIC variable. There are no new packages.

## Artist flow

1. Save the translation text for the selected language.
2. Press **Generate voice**. Only the saved text is sent to OpenAI. Each click is one paid request.
3. Listen to the whole recording. Check pronunciation, names and meaning. Generated voices can sound unnatural; upload your own recording instead if it is not good enough.
4. Approve the language through the existing flow. The feed then plays it labelled **AI voice, approved by {artist}**.

## Rules

- The saved translation must be non-empty and at most 1,800 characters (about two minutes). Longer text needs a hand-uploaded recording.
- Editing the translation text keeps the old voice. Press **Generate voice** again so it matches before approving.
- Generating a voice replaces that language's recording and revokes that language's approval, like an upload. Other languages keep their approval.
- The client sends the review version it displayed. If the story changed during generation, the API returns 409 and discards the voice. Existing content is unchanged.
- The recording is stored as mono PCM16 WAV at 24 kHz and marked with voice `standard`. The value `clone` is reserved for later and never set now.

## API and failure behaviour

`POST /api/stories/{id}/drafts/translations/{language}/ai-voice`, JSON body `{ "reviewVersion": "<current UUID>" }`, returns the saved story draft. The translation's `recordingVoice` is `standard`. Missing story returns 404, missing/oversized text 422, stale version 409, provider/configuration failure 502, and timeout 504.

The adapter matches tools/language-check: one Realtime WebSocket connection per click, voice `marin`, and instructions to read the passage verbatim in the target language. The style follows the story type: story (warm and slow, like a grandmother at night), folktale (lively, with pauses), poem (measured, pausing at line breaks) and song (gentle and rhythmic, spoken, not sung). Text is split at sentence ends into parts of at most 600 characters, each spoken as one response on the same connection. Multi-part speech (text over 600 characters) has not yet been tried live; listen for repeated or blended passages. There are no retries, a 3-minute timeout, and a two-minute audio cap. Any failure saves nothing. The key, upstream messages and raw exceptions are never logged or returned. A charged request cannot be undone by a failure, conflict or cancellation.

The simpler `/v1/audio/speech` endpoint was not used: its models are scheduled to shut down on 2027-01-06, and it does not support `gpt-realtime-2.1-mini`.

## Known limits

In the language test, a generated isiXhosa voice was rated good for pronunciation but bad for naturalness. OpenAI says its voices are optimised for English. There is no automatic check that the spoken words match the text; the artist's listening review is the check.

Official reference: [Realtime WebSockets](https://developers.openai.com/api/docs/guides/voice-websockets?api=realtime).
