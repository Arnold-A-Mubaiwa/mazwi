# Mazwi language feasibility

Issue #7 provides a development-only evaluation harness, not production API code.
The primary pair is **Shona (`sn`) and isiXhosa (`xh`)**. Swahili (`sw`) can replace either source for comparison; English is an optional reference.

## Current evidence

Offline tests and syntax/whitespace checks pass. The live evaluation is **BLOCKED** until an API key and two consented recordings are supplied.
No real recording has been sent, no voice quality has been scored, and no language is declared demo-ready.
Keep recordings, transcripts, and generated audio inside ignored `.local/` storage.

## Install and preflight

Run commands from the repository root using Node.js 24 or later:

```powershell
npm.cmd ci --prefix tools/language-check --ignore-scripts --no-audit --no-fund --cache .local/npm-cache
node tools/language-check/cli.mjs --help
node tools/language-check/cli.mjs
node --test
node .codex/skills/repo-quality/scripts/lint.mjs tools tests
```

The bare CLI command intentionally exits 2 with a BLOCKED preflight until configured.
Preflight does not contact OpenAI, read the recording contents, or create output files. READY checks file metadata only; it does not verify audio decodability, model access, or language support.

Set `OPENAI_API_KEY` in your process environment or an ignored `.env` / `.env.local` file. Process variables take priority over file values; `.env.local` takes priority over `.env`.
Do not put credentials in command arguments, chat, or tracked files. Environment files contain literal `NAME=value` assignments; shell expressions are never evaluated.

Provide one short recording per language, preferably 10–30 seconds of clear speech, with permission to send it to OpenAI.
Use `--sn` and `--xh` followed by their file paths and `--consent` to confirm permission for both recordings. Without `--live`, this still only performs preflight.
After checking the planned calls, add `--live` to send the clips and text to OpenAI and incur API charges. Add `--english` only if the extra translation/speech calls are wanted. `--sw` is available instead of either source, not as a third simultaneous source.
The CLI prints planned call counts before execution. Automatic API retries are disabled; rerunning live mode starts a new paid evaluation, not a resume.

## Model choices

Defaults checked against the official OpenAI documentation:

- Transcription: `gpt-transcribe`, using the current `languages` hints field.
- Translation: `gpt-6-luna` through Responses with a strict translation/notes JSON schema.
- Voice: `gpt-realtime-2.1-mini` through Realtime WebSocket events; mono PCM16 is saved as a 24 kHz WAV file. It is not sent to the legacy Speech endpoint.

Compatible model IDs can be set through `MAZWI_TRANSCRIPTION_MODEL`, `MAZWI_TRANSLATION_MODEL`, and `MAZWI_SPEECH_MODEL`. Access is account-dependent; the tool does not silently substitute models.
The existing spec's `gpt-4o-transcribe`/`whisper-1` models are deprecated, as is `gpt-4o-mini-tts`. A new model ID alone does not migrate the old speech workflow.

Sources: [file transcription](https://developers.openai.com/api/docs/guides/speech-to-text), [GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna), [speech migration](https://developers.openai.com/api/docs/guides/text-to-speech), [Realtime WebSockets](https://developers.openai.com/api/docs/guides/voice-websockets?api=realtime).

## Outputs and native-speaker review

Each live run creates a unique directory under `.local/language-check/`; existing runs are not overwritten. Outputs include:

- Source transcripts for both recordings.
- Original-language and translated-language text, translation notes, generated speech WAVs, and the Realtime spoken transcript.
- `review.json` with model IDs, processing failures, and **null/unreviewed** quality scores.

Generated speech is AI-generated standard voice, not a clone of the artist. Realtime narration may deviate from supplied text; compare its spoken transcript and listen to the WAV instead of assuming verbatim output.
For each language, a native speaker should score transcription, meaning, names/proverbs, pronunciation, and naturalness as **good / ok / bad**, record who reviewed it, and note corrections.
Choose demo audio only after review. If a voice is poor, use reviewed translated subtitles with clearly labelled original or fallback audio.
Successful API execution is not a quality pass: `qualityVerdict` stays `NOT_REVIEWED` until humans decide. Failed transcription prevents invented downstream translations; other independent clips/checks continue and failures are classified without printing raw API errors.

The tool caps input files at 25 MB, text at 6,000 characters, and each generated voice at two minutes. Longer material is outside this feasibility issue. File transcription can consume supported audio/video files directly; ffmpeg extraction is not needed for this short-clip test.
