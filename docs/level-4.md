# Level 4

Level 4 makes Mazwi an all-Africa tool: stories in 25 languages, the same tale told by different artists linked as **Story Twins**, a tested answer on **Voice Stamp**, and a demo judges can start without setting up a database.

| Feature | Issue / PR | Guide |
| --- | --- | --- |
| 25 African and shared languages from one catalogue | #57 / #63 | [languages.md](languages.md) |
| Story Twins: AI suggestions, artists confirm | #58 / #64 | [story-twins.md](story-twins.md) |
| Story Twins for viewers, and "ask for a version" | #59 / #65 | [story-twins.md](story-twins.md) |
| Voice Stamp feasibility test: **stop** | #60 / #68 | [voice-stamp-feasibility.md](voice-stamp-feasibility.md) |
| Voice Stamp | #61 | Closed without code: the test says stop |
| README for everyone | #66 / #67 | [README](../README.md) |
| Judge demo: no database setup | #69 / #73 | [README](../README.md#for-judges-try-it-with-no-setup) |

## The journey

1. A Yoruba artist uploads in **Yoruba**. The editor warns that AI quality in Yoruba is untested and asks for a speaker to check every AI draft.
2. A Shona artist tells the same tale in **Shona**.
3. Mazwi's background sweep makes a private English summary of each story and **suggests them as twins** in Studio. The Shona artist listens first, then confirms.
4. A Shona viewer sees **"Also told in Yoruba by …"** under the story. No Shona version exists yet, so they press **Ask for a Shona version**.
5. The Yoruba artist sees **"Listeners asked for a Shona version (1)"** in Studio. They translate the story into Shona and approve it.
6. The viewer now gets **Hear it in Shona**.

## How it is tested

`web/e2e/level-4.spec.ts` runs that journey through the UI against the real API, database, files and background twin sweep. Only OpenAI is replaced, by the local `e2e/fake-openai.mjs`, which the API honours only in the Testing environment on a loopback address. Each feature also has .NET tests on all four layers (with real PostgreSQL) and browser specs.

At the close of Level 4 there are 715 .NET tests, 112 web unit tests and 51 browser tests.

## Voice Stamp: why it was stopped

The feasibility test ran two open speaker-recognition models on 55 speakers of isiXhosa, Sesotho and Setswana. With 500 stamped artists, the bar was that at most 5% of honest uploads may be held. At that setting the best model missed the real artist's own voice 92% of the time, on clean studio audio. Building it would either hold most honest uploads or miss almost every real reuse of a voice. See [voice-stamp-feasibility.md](voice-stamp-feasibility.md) for the numbers, limits and what would change the answer.

## For the owner

- **Turning Story Twins on:** set `Mazwi__Twins__Enabled=true` in `.env` and restart. Each published story then costs two OpenAI calls (a gist and an embedding), and again whenever its approved words change. The threshold (0.8) is a guess: publish a few known twins and unrelated stories, see what gets suggested, and adjust `Mazwi__Twins__Threshold`. The embedding model default (`text-embedding-3-small`) was checked against OpenAI's model pages on 2026-11-11.
- **AI in new languages:** only Shona, isiXhosa, Kiswahili and English were used while building the AI drafts and voices. For the other 21, ask speakers to review real drafts before relying on them.
- **Judges:** `./scripts/Start-Mazwi.ps1 -Demo` needs only .NET and Node. It does not touch your `.env` databases.
- **Still pending from earlier levels:** claim your stories, add a real email provider, work through the hosting checklist, and run the Level 1 live OpenAI check.

## Release

After this merges, `level-4` is tagged (annotated) on the merge commit.
