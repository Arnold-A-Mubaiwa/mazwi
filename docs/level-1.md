# Level 1

Level 1 is the full artist journey with AI help: record in Shona, get an AI transcript, translation and voice, approve, and viewers watch it in their language. Nothing goes public without the artist pressing **Approve**.

## Turning it on

The three AI features (transcript first draft, translation draft, generated voice) are **on by default**. Turn any of them off by setting `NEXT_PUBLIC_MAZWI_AI_TRANSCRIPT`, `NEXT_PUBLIC_MAZWI_AI_TRANSLATION` or `NEXT_PUBLIC_MAZWI_AI_VOICE` to `false` in the build environment or ignored `web/.env.local`, then rebuild.

The API needs `OPENAI_API_KEY` in the ignored root `.env` or server environment. Without a key the buttons still show, but each click returns a safe "AI … is unavailable" message and nothing is saved; typing and uploading by hand keep working.

## What is automatic, and what the artist does

| Step | Automatic (one click, one paid request) | Artist |
|---|---|---|
| Upload | | Choose the recording, fill in details, confirm permission |
| Transcript | **AI first draft** writes the original-language text | Listen and correct names, praise names, proverbs and line breaks, then save |
| Approve original | | Press **Approve original** |
| Translation | **AI draft** translates the approved original, with notes on hard words | Read it if you can; fix what you know |
| Approve translation | | Press **Approve translation**. Tick **I speak … and checked this translation** only if true |
| Voice | **Generate voice** reads the translation aloud (up to about 2 minutes) | Listen to all of it, then approve the language again |
| Feed | Viewers pick a language and see the approved version | |

Every AI result is saved as an unapproved draft. Any edit, new AI result or new voice removes that approval until the artist approves again.

The feed is honest about what was checked:
- "AI voice, approved by <artist>" on generated audio.
- "Approved by <artist>. Not checked by an isiXhosa speaker." when the speaker box was left unticked, or "Approved by <artist>, who checked the isiXhosa." when ticked.
- Original-language stories show "Artist-approved <language> version."

## Cost per story

Prices from OpenAI's official [pricing page](https://developers.openai.com/api/docs/pricing), read 2026-11-10. Example: a 2-minute Shona recording translated into one language with a generated voice.

| Feature | Model (default) | Price | Example |
|---|---|---|---|
| Transcript | `gpt-transcribe` | $0.0045 per audio minute | 2 min ≈ **$0.01** |
| Translation | `gpt-6-luna` | $0.10 input / $0.50 output per 1M tokens | ~3,000 in + ~2,000 out ≈ **$0.002** |
| Voice | `gpt-realtime-2.1-mini` | $20 audio output / $0.60 text input per 1M tokens | 2 min ≈ **$0.05** (estimate) |

About **$0.06 per story**, plus about $0.05 for each extra language with a voice. Every click is charged again, including retries, regenerations and requests that later fail or are cancelled.

The voice figure is an estimate: the pricing page gives no audio tokens-per-minute figure, so it assumes about 1,200 output tokens per minute of speech, the ratio OpenAI gave when it launched Realtime audio pricing. Check the OpenAI usage dashboard after the first live story and correct this table.

## Known limits

From the language test (docs/language-feasibility.md) and the AI feature docs:
- **Shona transcripts failed human review** in the live test. Expect to correct a lot; the AI draft saves typing, it is not a transcript.
- **Translations were unreliable** when built from uncorrected transcripts. They improve from a corrected, approved original, but an artist who does not speak the target language cannot check them; the feed says so.
- **isiXhosa voice**: pronunciation was rated good, naturalness bad. OpenAI says its voices are optimised for English. Nothing checks that the spoken words match the text except the artist listening.
- **Long voices**: text over 600 characters is spoken in parts; this has not been tried live. Listen for repeated or blended passages. Voices stop at 1,800 characters (about 2 minutes).
- **Video originals**: OpenAI lists `.mp4` but does not say video files are accepted for transcription. Check one on the first live run.
- **Timeouts**: each AI request has a 3-minute timeout; large recordings may need more.
- OpenAI's pages do not mention Shona or isiXhosa support.

## How it is tested

`web/e2e/level-1.spec.ts` runs the whole journey in a browser against the real API and database: upload → AI transcript → correct → approve original → AI translation → approve (speaker box unticked) → generate voice → approve → feed in isiXhosa with the "AI voice" and "Not checked by an isiXhosa speaker" labels. Only the three OpenAI calls are faked; the fakes save equivalent text and a recording through the real API, and mark the voice as AI-generated (the database side of that marker is covered by the API and repository tests). It costs nothing and needs no key.

## Release

After this merges, the owner runs one live story with a real key, checks the costs and limits above, then tags `level-1`.
