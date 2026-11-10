# Mazwi language feasibility

This page documents the development evaluation tool and its language-quality limits. The app now has opt-in AI transcript first drafts (docs/ai-transcript.md), translation drafts (docs/ai-translation-drafts.md) and voices (docs/ai-voice.md).
Primary languages: Shona (`sn`) and isiXhosa (`xh`); Swahili (`sw`) and English (`en`) are also accepted.

## Evidence and limits

The first live automatic evaluation completed its API requests but **failed human review**: source transcripts were incorrect and downstream translations were unreliable. API access works; language quality is not established. Existing private outputs are retained as evidence, not demo content.

Issue #21 replaces the automatic pipeline with separate stages. The user approved the isiXhosa text produced from their corrected Shona reference and English meaning. A subsequent generated voice was rated pronunciation good but naturalness bad, and rejected for demo use. This short example does not establish quality for other stories or languages.

Recordings, reference text, approvals and generated files stay in ignored `.local/language-check/`. Nothing is published to the app. These local review records are an operator workflow, not authenticated identities or tamper-proof proof of consent.

## Verified checks

Run from the repository root:

```powershell
node tools/language-check/cli.mjs --help
node --test
node .codex/skills/repo-quality/scripts/lint.mjs tools tests
```

Offline tests exercise all stages with fake services, including blocked calls, stale approvals, wrong-language flags, protected names, audio changes and request failures. They do not establish real pronunciation quality.

## Stage workflow

The following are CLI argument templates; replace angle-bracket values with local files or reviewer details. Each invocation runs exactly one stage and writes a new private output directory when executed.

1. **Transcribe:** `--stage transcribe --sn <recording>` (optionally a second source with `--xh`, `--sw` or `--en`). Add `--consent --live` to send recordings. This stage only transcribes; it never translates or generates speech.
2. **Review the source:** `--stage review-transcript --input <transcript.json> --text <corrected.txt> --meaning <English.txt> --terms <names.json> --language sn --reviewer <person> --decision approve --apply`. Corrections, English meaning and protected terms are optional for an existing transcript. Import a verified human reference instead with `--source sn --text <verified.txt>` and no `--input`. Terms are a JSON array of exact names/places.
3. **Translate text only:** `--stage translate --input <approved-transcript.json> --target xh --consent --live`. An unapproved, rejected or changed source blocks before any API call. Translation uses the verified English meaning as context and requests exact preservation of protected names/places.
4. **Native review:** `--stage review-translation --input <translation.json> --language xh --reviewer <native-speaker> --decision approve --apply`. Use `--text <corrected.txt>` for corrections. Wrong-language, copied-source, missing-name or uncertainty flags require corrected text before approval. The reviewer must actually understand the target language; do not infer approval from an API response.
5. **Generate voice:** `--stage voice --input <approved-translation.json> --consent --live`. Only an approved exact translation is accepted. This generates standard AI voice, not a clone. It does not mark audio demo-ready.
6. **Listen and review:** `--stage review-voice --input <voice.json> --language xh --reviewer <native-speaker> --decision approve --pronunciation good --naturalness good --apply`. Acceptable ratings are `good` or `ok`. Listen to the saved WAV, check meaning and pronunciation, and reject poor output with `--decision reject`. Spoken-text mismatch blocks approval; regenerate rather than editing audio metadata.

Pass all arguments to `node tools/language-check/cli.mjs`. Run `--help` for syntax. Without `--live` or `--apply`, the tool only checks readiness. Human review is offline and needs no API key. `--apply` records a real human decision; never use it to manufacture approval of unreviewed content.

Approvals bind exact saved content with SHA-256, including embedded upstream approvals. Edits require a fresh review. Voice review also verifies the WAV hash. Later artifacts retain snapshots of reviewed inputs; changing an older file does not rewrite existing snapshots. Legacy automatic reports cannot grant approvals.

## Models and configuration

Defaults remain `gpt-transcribe`, `gpt-6-luna` (Responses structured output), and `gpt-realtime-2.1-mini` (Realtime WebSocket, mono PCM16 WAV at 24 kHz). No silent substitution. Override with `MAZWI_TRANSCRIPTION_MODEL`, `MAZWI_TRANSLATION_MODEL`, or `MAZWI_SPEECH_MODEL` only for compatible models available to your account.

Transcription sends the minimal documented `file` and `model` fields. Earlier requests containing language/prompt hints failed in live testing; the exact offending hint was not isolated. Audio-only M4A files with doubled `.m4a.mp4` names are recognized from their header and uploaded with audio metadata.

Set `OPENAI_API_KEY` only in your environment or ignored `.env` / `.env.local`. Process variables take priority; `.env.local` overrides `.env`. Shell expressions are never evaluated. Never paste keys into chat or tracked files. Network stages require consent and incur API charges. Automatic retries are disabled; rerunning a network stage creates another paid request.

The tool allows one or two recordings, each up to 25 MB, text up to 6,000 characters, and generated speech up to two minutes. Native review is mandatory: model-reported language and uncertainty checks are warnings, not independent proof of correctness. Use reviewed text with original audio if synthetic pronunciation is poor.

## Reading the report

`processingStatus: PASS` means the selected stage completed. `qualityVerdict: NOT_REVIEWED` and `demoReady: false` remain until the exact generated voice passes native review. An explicit rejection reports quality `FAIL`. Source or translation approval alone never makes audio demo-ready. No automatic progression occurs between stages. Failures are classified without printing raw API errors, credentials or private text.

Official references: [transcription quickstart](https://developers.openai.com/api/docs/guides/speech-to-text#quickstart), [Responses](https://developers.openai.com/api/reference/resources/responses), [Realtime WebSockets](https://developers.openai.com/api/docs/guides/voice-websockets?api=realtime).
