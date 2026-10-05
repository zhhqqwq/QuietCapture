# G0-2 — System Audio

## Question

Can ScreenRecorderLib 7.0.1 reliably record Windows system audio from one explicitly selected loopback output device while producing a normal MP4 video?

**Gate status:** NOT RUN

## API under test

ScreenRecorderLib 7.0.1 changed audio configuration. The harness uses:

- `Recorder.GetSystemAudioLoopbackDevices()` to enumerate output devices;
- the selected `RecordableAudioLoopbackDevice.DeviceName` as the concrete device ID;
- `new LoopbackAudioSource(deviceId)` in `AudioOptions.AudioSources`;
- `AudioOptions.IsAudioEnabled = true`;
- stereo AAC audio at 128 kbps;
- `OnAudioPacketRecorded` for packet/byte evidence.

No microphone source is added in G0-2.

## Test matrix

| Case | Setup | Purpose |
| --- | --- | --- |
| B1 | selected default output device; continuous audible playback | baseline system-audio recording |
| B2 | silence → audible playback → silence | verify silent intervals and resumed audio |
| B3 | explicit non-default output device, when available | verify concrete device binding |
| B4 | change Windows default output during recording | verify the session does not silently rebind |
| B5 | Stop normally, then start a second run with the same selected ID | verify repeatable initialization/Stop |
| B6 | short run with no intentional playback | observe track/packet behavior during silence |

## Evidence

Each run writes:

- `*.mp4`;
- `*.log` from ScreenRecorderLib;
- `*.g0-2.json` containing selected device ID, enumeration snapshot, recorder states, audio packet counts, byte counts, and Stop/completion timestamps.

Audio packet counts are supporting evidence only. A PASS decision still requires listening to the recorded track and checking the media container on a real Windows machine.

## Pass criteria

Mark PASS only if the selected output device is recorded reliably, no microphone source appears, normal Stop finalizes playable MP4 output, and changing the Windows default does not silently move an active session away from its explicitly selected device.

Use PASS WITH WORKAROUND for a bounded device-specific or configuration workaround.

Use FAIL if system audio cannot be made reliable enough for the v1 flow.

Summarize reviewed results in `RESULTS.md` and `docs/technical-spike.md`.
