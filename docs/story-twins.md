# Story Twins

A Shona artist tells *Tsuro na Gudo*; a Yoruba artist tells the same tale with Ìjàpá. Story Twins helps the two find each other, and later helps viewers move from one telling to the other (#59).

## How it works

1. An artist **approves the original** of a story; it is published.
2. A background sweep (every minute by default) finds published stories with no fingerprint, or whose approved words changed.
3. For each one, Mazwi asks OpenAI for a **private English plot summary** (the characters by name, the events, the ending), then an **embedding** of that summary.
4. It compares the story with every other published story by **another artist** in **another language**. Pairs at or above the threshold become **suggestions**, up to five per story.
5. Both artists see the suggestion in **Studio → Story Twins**, with a link to listen first. Either artist can:
   - **Yes, same story**: the pair is linked.
   - **Not the same**: the pair is never suggested again.
   - **Unlink** a linked twin at any time; that is also final.
6. Nothing is shown to viewers until an artist confirms.

## For viewers (#59)

- Under a story, **Same story, another voice** lists its confirmed twins, for example "Also told in Yoruba by ABS (Yoruba, Nigeria)".
- If the twin's artist approved a version in the viewer's language, the button says **Hear it in Shona** and the twin plays in Shona.
- Otherwise the card says so honestly, offers **Hear it in Yoruba** (with its approved words), and **Ask for a Shona version**.
- Requests are anonymous counts per story and language: nothing about the listener is stored, and they are rate-limited per address (`Mazwi:VersionRequestsPerMinute`, default 10).
- The twin's artist sees "Listeners asked for a Shona version (3)" on the story's Studio card until they approve a Shona version.

## Artist control

- Stories take part by default. In the story editor, **Let Mazwi suggest twins for this story** turns it off. Off also hides existing twins (for both artists); turning it back on brings them back.
- Unpublished stories, stories hidden by moderators and stories without an account (imported, not yet claimed) never take part.
- Summaries and embeddings never leave the server: no API response contains them, or the similarity score.
- Deleting a story deletes its fingerprint and its pairs.

## Turning it on

It is **off by default**, because each published story costs two OpenAI calls the artist did not click for (and again whenever its approved words change).

```
Mazwi__Twins__Enabled=true
OPENAI_API_KEY=...            # or Mazwi__Twins__ApiKey to use a separate key
Mazwi__Twins__Threshold=0.8   # optional
Mazwi__Twins__SweepSeconds=60 # optional
MAZWI_EMBEDDING_MODEL=text-embedding-3-small  # optional; the summary uses MAZWI_TRANSLATION_MODEL
```

Without a key, or when off, nothing is suggested and everything else works as before. A failed call is retried after 30 minutes.

## The threshold is uncalibrated

`0.8` is a starting guess, not a measured value. Before relying on suggestions, publish a few known twins and a few unrelated stories, look at which pairs appear, and adjust `Mazwi__Twins__Threshold`. Too low and every animal folktale looks alike; too high and real twins in very different tellings are missed. Artists confirming every pair is what keeps mistakes away from viewers.

## Scale

Each sweep compares new fingerprints with all current ones in memory. That is fine for thousands of stories; beyond that, move the comparison into the database (for example pgvector).

## Tests

- Domain: pair ordering, the confirm and reject lifecycle, thresholds, fingerprints, and matcher rules (other artist, other language, best five).
- Application: the sweep, recording failures, and confirm, reject and opt-out.
- Repository (real PostgreSQL): which stories need fingerprints, stale and failed fingerprints, final rejection, eligibility on every read, and cascade on delete.
- WebApi: the OpenAI adapter (requests, failures, no leaks), the settings (the fake base URL is honoured only in Testing on loopback), and endpoints with real PostgreSQL. The tests check that no gist reaches a response and that only the owner can act.
- Browser (`e2e/twins.spec.ts`): the real API, database and background sweep, with only OpenAI replaced by the local `e2e/fake-openai.mjs`. Two artists get a suggestion, reject one, confirm one, opt out and back in, and unlink.
