# Voice Stamp feasibility

**Question (#60):** can Mazwi "stamp" an artist's voice, so that an upload by a different artist using that voice is held for moderation, using an on-device speaker-recognition model?

**Answer:** see [Recommendation](#recommendation).

## Decision rule (written before the full run)

The rule was fixed before looking at the final numbers, so the result cannot be tuned towards a nicer answer.

**Operating point.** With 500 stamped artists, at most **5%** of uploads by people without a stamp may be wrongly held. That needs a per-pair false-match rate of about **1.0 × 10⁻⁴**, because each upload is compared with every stamp: 1 − (1 − p)⁵⁰⁰ ≤ 5%.

**Method.**
1. Speakers are split into a **calibration** half and an **evaluation** half (different people).
2. The threshold for that per-pair rate is chosen on the calibration half.
3. Every number below comes from the evaluation half.

**Build #61 only if, for at least one model and scoring method, at that threshold:**

| Upload | Missed own voice (artist's own upload not recognised) |
| --- | --- |
| Clean recording | at most **10%** |
| First 3 seconds only | at most **20%** |
| White noise at 10 dB SNR | at most **30%** |

In addition, the evaluation per-pair false-match rate must not clearly exceed the target.

**Otherwise stop:** #61 is closed without code, linking here.

## What was tested

- **Runtime:** [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) 2.0.1 for Node (Apache-2.0). It bundles ONNX Runtime and Kaldi-style filterbank features, on CPU with 2 threads.
- **Models** (open speaker-embedding models from the sherpa-onnx `speaker-recongition-models` release):

| Model | Licence | Size | SHA-256 |
| --- | --- | --- | --- |
| 3D-Speaker CAM++ (VoxCeleb, English) `3dspeaker_speech_campplus_sv_en_voxceleb_16k.onnx` | Apache-2.0 (3D-Speaker) | 29.6 MB | `357a834f702b80161e5b981182c038e18553c1f2ca752ed6cec2052365d4129b` |
| WeSpeaker ResNet34-LM (VoxCeleb) `wespeaker_en_voxceleb_resnet34_LM.onnx` | CC BY 4.0 (inherits VoxCeleb's licence, per WeSpeaker) | 26.5 MB | `e9848563da86f263117134dfd7ad63c92355b37de492b55e325400c9d9c39012` |

- **Recordings:** OpenSLR 32 isiXhosa (`xh_za`), multi-speaker studio recordings, CC BY-SA 4.0. They were downloaded, used locally in `.local/voice-stamp` and never committed.
- **Stamps:** each stamped artist enrols with 3 clean clips, and the stamp is the mean embedding. About 30% of speakers are never stamped, standing in for honest uploaders.
- **Uploads:** each is compared with every stamp in its half:
  - clean;
  - the first 3 seconds only;
  - white noise at 10 dB and 5 dB SNR, with a different noise per clip.
- **Scoring:**
  - **raw** cosine;
  - **centred** cosine: the mean of all enrolment embeddings is subtracted first. This is a standard fix when every recording shares a microphone and room.
- Every clip is resampled to 16 kHz and its silent edges are trimmed.

## Results

_Filled in from the final run; see below._

## Limits of this test (they cut both ways)

- **Best case for "same voice".** The data is a studio text-to-speech corpus: one microphone, same-session takes per speaker. Mazwi's real case is cross-session and cross-device, such as a phone video months later compared with a stamp. Real same-speaker scores will be **lower** than here.
- **Inflated "other voice" scores.** Everyone shares the same room and microphone, so different speakers score higher than they would elsewhere. Centring compensates for part of this.
- **Not tested at all:**
  - family members and twins, who sound alike;
  - AI voice clones, which are the real threat Voice Stamp is for;
  - singing, poems and performance voices;
  - music and crowd noise under speech;
  - compressed phone audio (AAC/Opus);
  - any African language other than isiXhosa.
- **Small numbers.** Few speakers means few impostor pairs. A per-pair rate near 10⁻⁴ cannot be measured directly with a few thousand pairs; the calibrated threshold is the highest other-speaker score seen, so the real false-match rate at that threshold is probably **higher** than measured.
- **White noise is a crude stand-in** for real background noise.

## What real recordings a real decision would need

- At least **200 speakers**, each with **5 or more recordings from at least 2 sessions and 2 devices** (a phone and a laptop), in several of Mazwi's languages.
- **Family groups**: siblings, parent and child.
- Performance styles: storytelling, poems, singing.
- **AI voice clones** of consenting speakers, made with current cloning tools, as the attack case.
- Consent for biometric use from every speaker. Under POPIA (South Africa), the Kenya DPA, the NDPA (Nigeria), GDPR and similar laws this is special-category data.

## Data protection notes

A voiceprint is **biometric data**. If Voice Stamp is ever built:
- It needs **explicit, separate opt-in consent**, and the purpose is limited to protecting that artist's voice on Mazwi.
- Keep the **embedding only**, never the enrolment audio.
- Delete it with the account or on request, and say how long it is kept.
- An artist's own uploads, including their own AI voice versions, must never be held against them.
- A match must only ever **hold for human moderation** with the reason shown, never auto-remove or accuse.
- The comparison must stay **on Mazwi's server**. No voiceprint is sent to third parties.

## Engineering cost if built

- `Mazwi.Ai` takes no packages today. Voice Stamp would need **ONNX Runtime**, a native package, plus Kaldi-style filterbank features in C# or a sidecar process (for example this Node tool). This needs the owner's approval.
- Uploads are MP3, WAV, M4A, MP4 and WebM. There is **no audio decoder** on the server today; it would need ffmpeg or a similar decoder.
- The models are 26–30 MB and take about 0.1 s of CPU per clip (see speed below), so cost is not the obstacle.

## Reproduce

```
cd tools/voice-stamp-check
npm install --no-audit --no-fund
# download models (see SHA-256 above) into .local/voice-stamp/models
# download and extract https://www.openslr.org/resources/32/xh_za.tar.gz into .local/voice-stamp/xh
node --test core.test.mjs
node cli.mjs --model ../../.local/voice-stamp/models/<model>.onnx --wavs ../../.local/voice-stamp/xh/xh_za/za/xho/wavs --out ../../.local/voice-stamp/<model>.json
```

## Recommendation

_Decided by the rule above once the final run is in._
