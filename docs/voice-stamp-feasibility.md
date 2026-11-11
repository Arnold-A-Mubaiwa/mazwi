# Voice Stamp feasibility

**Question (#60):** can Mazwi "stamp" an artist's voice, so that an upload by a different artist using that voice is held for moderation, using an on-device speaker-recognition model?

**Answer: no, not with today's open models. Stop. #61 is closed without code.** See [Recommendation](#recommendation).

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

The run used 2026-11-11 data and covered **55 speakers** with **595 clips** (OpenSLR 32: isiXhosa, Sesotho and Setswana; up to 12 clips per speaker).
- **Calibration:** 20 stamped artists and 8 unstamped uploaders.
- **Evaluation:** 19 stamped artists and 8 unstamped uploaders, all different people from the calibration half.
- Each upload in the evaluation half was compared with 19 stamps: 133 own-voice pairs and 4,028 other-voice pairs.

"Missed own voice" is measured at the threshold chosen on the calibration half for the operating point: 5% wrongly held at 500 stamps.

**WeSpeaker ResNet34-LM** (256 dimensions, about 0.1 s CPU per clip, real-time factor 0.03):

| Upload | Scoring | EER | Threshold | Missed own voice | Per-pair false match |
| --- | --- | --- | --- | --- | --- |
| clean | raw | 9.9% | 0.920 | **91.7%** (122/133) | 1/4028 |
| clean | centred | **4.4%** | 0.830 | **98.5%** (131/133) | 0/4028 |
| short 3 s | raw | 12.2% | 0.922 | 96.2% (128/133) | 1/4028 |
| short 3 s | centred | 6.2% | 0.773 | 96.2% (128/133) | 0/4028 |
| noisy 10 dB | raw | 36.1% | 0.801 | 100% (133/133) | 0/4028 |
| noisy 10 dB | centred | 33.8% | 0.664 | 100% (133/133) | 0/4028 |
| noisy 5 dB | raw / centred | 40.6% / 41.3% | ~0.7–0.8 | 100% | 0/4028 |

**3D-Speaker CAM++** (512 dimensions, about 0.1 s CPU per clip) did worse on this data, in every condition and with both scorings:
- EER 30–47%.
- Missed own voice 99–100%.

What the numbers mean:
- **The models can tell people apart, but not strictly enough.** The best pair-by-pair error is 4.4% (EER). That would be good for "are these two clips the same person?". Voice Stamp, though, compares every upload with **every** stamp.
- **At the EER threshold, an honest upload would be wrongly held** in about 1 − 0.956⁵⁰ ≈ **90%** of cases with only 50 stamped artists.
- **Raising the threshold until honest uploads are safe misses the real artist's own voice 92–99% of the time**, even on clean studio audio from the same session.
- **Noise breaks it completely.** At 10 dB SNR, roughly a busy street or a phone in a room, the EER is 34–36%.
- The per-pair false-match target of 10⁻⁴ cannot be measured with 4,028 pairs, so the chosen thresholds are "above the highest other-voice score seen". This only makes the real picture **worse**, not better.

**Decision rule result:**

| Rule | Required | Best measured |
| --- | --- | --- |
| Clean recording | ≤ 10% missed | 91.7% |
| First 3 seconds | ≤ 20% missed | 96.2% |
| 10 dB noise | ≤ 30% missed | 100% |

All three fail, and they fail by a wide margin. This is the **easy** case: studio audio, same session, no clones.

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

**Stop.** Do not build Voice Stamp (#61) now. With open on-device models it would either hold most honest uploads, or miss almost every real reuse of a voice. Either outcome would hurt artists and erode trust in moderation. #61 is closed without code, linking here.

What would change the answer:
- **Better data.** Run this tool on the recordings listed above: many speakers, several sessions and devices, family members and AI clones. If a model then passes the rule, revisit.
- **A narrower product.** Instead of comparing every upload with every stamp, compare an upload only with **one named artist**: for example, when someone reports "this uses my voice", or when an upload claims to be by a stamped artist. That is a 1-to-1 check, where a 4–10% EER is far more usable. It would still only be a hint for a human moderator. This is a new idea for the owner to decide on, not part of Level 4.
- **Keep watching voice-clone detection** and audio watermarking: marking Mazwi's own AI voices so they can be recognised later. These attack the clone problem more directly than speaker recognition.

Meanwhile, Mazwi's existing protections apply:
- uploaders confirm that they own the recording or have permission;
- AI voices are standard voices, never clones of the artist;
- viewers can report "Shared without permission" for moderators.
