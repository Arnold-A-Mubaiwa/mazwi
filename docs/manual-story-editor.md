# Manual transcripts and translation drafts

Issue #13 adds the artist editing page at `/story/<story-id>/review`.
It uses the .NET API and PostgreSQL without sending anything to OpenAI.

## Try it

1. Start the local API and website using docs/frontend-setup.md. The API terminal must load the ignored `.env` first.
2. Upload a permitted recording at `/upload`.
3. On the unpublished confirmation, choose **Write transcript and translations**.
4. Listen to the original on the editing page, type its words, and choose **Save original transcript**.
5. Choose another supported language, type or paste your own permitted translation, and choose **Save translation draft**.
6. Bookmark the editing URL. Reopening it loads saved text and translations from PostgreSQL.

Supported languages are Shona, isiXhosa, Kiswahili and English. The original language cannot be a translation target.
Each text must contain 1–20,000 characters after trimming its outer whitespace. Internal line breaks, tabs, names and punctuation are preserved; other control characters are rejected.
An original transcript must be saved before translations. Save changes to the original before editing translations.
Changing the original preserves existing translation drafts; the artist must check them again for meaning.
Automatic translation, generated voices and accounts remain pending. Issue #15 adds explicit artist approval and local recording playback; see docs/artist-approval.md.

## Save behavior

Each save changes only the selected transcript or translation, preserving other languages.
Saved or changed translations are drafts until explicit artist approval. Supplying an approval status in a text-save request cannot approve it. Original approval makes a story eligible for viewer publication; source changes revoke it.
Failed saves keep typed text in the editor for retry. A browser warning helps avoid accidentally closing a page with unsaved changes; also keep a copy before navigating away.
Text is not autosaved, and unsaved text is not backed up in localStorage. Each editing session must explicitly save.
Concurrent editing is last-write-wins per field; collaboration and conflict detection are future work.
Stories and draft metadata use the owner's temporarily shared local test database; browser test files remain separate from artist recordings.
This is an unauthenticated localhost demo. The editing URL is not access control; do not expose the app publicly.

## Backend

- `GET /api/stories/{id}/drafts`: unpublished story information, transcript and saved translations.
- `PUT /api/stories/{id}/drafts/transcript` with `{ "text": "..." }`: save the original transcript.
- `PUT /api/stories/{id}/drafts/translations/{language}` with `{ "text": "..." }`: save one translation draft.

Missing stories return 404, invalid input returns 400 and unexpected failures return sanitized errors.
Domain value objects and aggregate methods enforce text/language/state rules. Application services orchestrate Domain repository interfaces; SQL stays parameterized in Repository.
Migration `003_add_story_text_drafts.sql` adds the nullable transcript and a table keyed by story and target language.
No recording contents, disk paths or secrets are exposed by these text endpoints.
The `manualEditing` feature flag controls the page and upload link; `translations` remains disabled for future AI processing. Flags do not provide authorization.

## Verification

Verified for this feature: 158 .NET tests, 39 offline Node tests (including 16 frontend unit tests), and 15 Edge browser tests.
The .NET build has zero warnings; format, architecture, frontend lint, type-check and production build pass.
Browser coverage includes saving/reopening the original and two languages, preserving drafts after source changes, failed-save recovery, failed-load retry, missing stories, the upload-to-editor link and mobile layout.
Screenshots of the editor were reviewed on desktop and mobile. Tests use synthetic text; they do not establish translation accuracy or language/voice quality.
