# AI translation drafts (Step 1)

The story editor can request a translation of the saved, approved original transcript into its selected language. It saves the result as a normal editable draft with short notes. It never approves or publishes the translation automatically and generates no audio.

## Enable locally

`FEATURES.aiTranslation` reads the public build-time boolean `NEXT_PUBLIC_MAZWI_AI_TRANSLATION`. Since Level 1 it is on unless the value is exactly `false` (see docs/level-1.md). To turn it off, set NEXT_PUBLIC_MAZWI_AI_TRANSLATION=false in the build environment or ignored web/.env.local, then rebuild the frontend. Next.js bundles public variables at build time; changing only the runtime environment does not change an existing build. To disable it again, unset the flag and rebuild. Manual editing works with the flag off and without an API key.

The API reads `OPENAI_API_KEY` and `MAZWI_TRANSLATION_MODEL` from server configuration. The model defaults to the language tool's `gpt-6-luna`; no model substitution occurs. Keep the key in ignored root .env or the server environment, never in a NEXT_PUBLIC variable. The startup helper loads root .env. Restart managed services to reload server settings. There are no new packages.

## Artist flow

1. Upload a recording with permission. Listen, enter/correct the original transcript, save it, then approve it.
2. Choose a different translation language. Save all pending edits first. Press **AI draft** only when you want the approved transcript sent to OpenAI; each click can incur a charge.
3. Wait for the saved draft. Review its language, meaning, names, proverbs and poem line breaks. The notes describe cultural choices or uncertainty; they are suggestions, not evidence of correctness.
4. Edit the translation and notes. Use **Dismiss note** to remove a note, then **Save translation draft** to persist changes.
5. Explicitly approve through the existing flow only after checking the saved result. If an older translated recording is attached, listen and replace it if the wording changed. This step does not generate or replace recordings.

## Approval and concurrent edits

AI drafting requires a saved approved original, a different supported target, and at most 6,000 source characters. Longer text remains editable manually up to 20,000 characters. Generated text is validated against that manual limit; notes allow up to 20 non-empty entries of 1,000 characters each.

An AI save revokes that language's approval even if regenerated text/notes are identical. Manual unchanged saves retain approval; changing text or notes revokes it. Other language approvals remain unchanged. Source edits still revoke all approvals. Notes survive approvals, recording changes, source edits and reloads.

The client sends the review version it displayed. The API checks it before generation and the repository checks it again under a database row lock before saving. If the source or any draft/recording changes during generation, the API returns 409 and discards the AI result. Existing edits are preserved; reload and review before retrying. A charged request cannot be undone by a conflict or cancellation.

## API and failure behaviour

`POST /api/stories/{id}/drafts/translations/{language}/ai-draft`, JSON body `{ "reviewVersion": "<current UUID>" }`, returns the saved story draft including the selected translation's text, notes and draft status. Missing story returns 404, unmet prerequisites 422, stale version 409, provider/configuration failure 502, and timeout 504. Missing/malformed JSON follows ordinary API validation.

The adapter sends one HTTPS Responses request, with no retries or redirects, a 90-second timeout, low reasoning effort and a 4,096-token output budget. Structured output has exactly `translation` and `notes`. It rejects refusals, incomplete output, invalid notes/text and oversized responses. Cancellation propagates through HTTP and database work. Safe failures preserve saved content; the adapter does not log provider bodies, credentials or raw exceptions, and retains no upstream inner exception.

Only the saved source is sent, not unsaved text or the recording. The current app has no saved English-reference/protected-term fields from the evaluation tool; the prompt instructs preservation of names rather than claiming a verified name list. Native-speaker review remains essential. This remains a localhost demo without accounts; keep it local. One click requests one paid draft, not a fixed price per story; live cost/quality measurement is deferred to the owner's Level 1 check.

## Validation scope

Tests use fake drafters/HTTP responses and a faked browser API, plus real local PostgreSQL tests for notes and stale saves. No paid OpenAI calls are needed for automated validation. The feature-enabled browser test exercises generation, note editing/dismissal and explicit approval; the default-off build verifies the button is hidden.

Official schema reference: https://developers.openai.com/api/docs/guides/structured-outputs#structured-outputs-vs-json-mode
