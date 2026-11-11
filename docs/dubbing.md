# Dubbed video playback

Issue #35 lets viewers watch a video story with its approved translated voice in place of the original sound. It is done in the browser; no file is re-encoded and no AI is called.

## When it appears

The viewer plays a story dubbed when all of these are true:
- The original recording is a video (`.mp4` or `.webm`).
- The viewer's language has an approved translation with its own **audio** recording (uploaded by the artist or generated as an AI voice).

The label says whose voice it is, for example "Dubbed: AI voice, approved by Rudo. The original video plays without its sound."

**Watch with original sound** switches to the original video and sound, and **Watch dubbed** switches back. A translation whose own recording is a video plays as it is, without dubbing.

## How it works

- `components/dubbed-player.tsx` plays the original video and the translated voice together. The video is the clock: play, pause, seek and playback speed are mirrored onto the voice.
- The voice is moved back whenever it drifts more than 0.3 s from the video (`lib/dubbing.ts`).
- The video's own soundtrack is routed into a Web Audio node that is never connected to the speakers, so the video's volume and mute controls stay usable for the voice. Without Web Audio, the video is muted instead.
- If the video ends first, a longer voice finishes its last words. A shorter voice simply stops.
- Artist-timed subtitles for the translation follow the voice, because they were timed against that recording.

## Tests

- `tests/dubbing.test.mjs` covers when dubbing applies, the label, and the drift rules at their boundaries.
- `e2e/dubbing.spec.ts` records a short WebM video in the browser (there is no ffmpeg) and uploads it with a 2-second translated WAV. It checks:
  - the voice plays while the video plays and stays within 0.35 s of it;
  - pausing the video pauses the voice;
  - seeking the video moves the voice;
  - the original-sound toggle works both ways.

## Limits

- There is no lip-sync, and the translated voice is rarely the same length as the video.
- There is no downloadable dubbed file. Server-side export needs ffmpeg, which is not installed.
- Autoplay rules mean a viewer may have to press play once.
- On slow connections the voice may pause briefly while it buffers; it resyncs when the video plays again.
