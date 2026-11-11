# Unpublishing and deleting

Issue #46 gives artists full control over what they have shared. Only the story's owner, signed in, can do any of this.

## Unpublish a story

In the story editor, **03 / Your story, your choice** has **Unpublish story** while the story is published. It removes every approval, so the story leaves the feed and its recordings are no longer served to viewers. Words, translations, recordings and subtitle timings are kept, and approving the original again republishes it.

## Delete a story

In the same panel, type the story's title (case and spacing are forgiven), then press **Delete story forever**. This permanently removes:
- the story, every translation, approval, subtitle timing and play count (database rows, removed together);
- every recording file: the original and each translated or AI voice recording.

The database rows are deleted first, then the files. If the database step fails, nothing is lost. If a file deletion fails afterwards, the file is left as private clutter that no route can serve.

The request carries the story's review version, so a page left open while the story changed elsewhere gets "This story changed…" instead of deleting something the artist hasn't seen. Copies viewers already downloaded cannot be recalled.

## Delete your account

**Account** in the header leads to `/account`. Enter your current password and tick "I understand…" to delete your account **and every story you uploaded**, with all recordings, in the same order as above. You are then signed out, and the email can sign up again as a new, empty account.

## API

| Endpoint | Purpose |
| --- | --- |
| `POST /api/stories/{id}/unpublish` | `{ reviewVersion }`; 400 if not published, 409 if changed |
| `DELETE /api/stories/{id}?version=` | 204 deleted, 404 missing, 409 changed |
| `POST /api/account/delete` | `{ password }`; 204 and sign-out, 400 wrong password; rate-limited like sign-in |

All are owner-only: signed out gets 401, another artist's story gets 403.

## Tests

- **Domain:** unpublishing rules and listing every recording file.
- **Application:**
  - rows are deleted before files;
  - a stale version or a failed row delete keeps the files;
  - account deletion checks the password, deletes every story then the artist, and retries a story that changed mid-delete.
- **Repository (real PostgreSQL):** unpublish leaves the feed; delete removes every child row; stale versions throw.
- **Endpoints:**
  - status codes and owner checks;
  - a real database round-trip: delete a story, a wrong password, then delete the account, after which no files remain, the session has ended and sign-in fails.
- **Browser:** `e2e/removal.spec.ts` unpublishes, confirms by title, deletes and checks both recording files are gone. It also signs up a new artist, uploads, deletes the account and checks the story's file is gone and sign-in fails.
