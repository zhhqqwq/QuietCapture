# G0-3 — System Audio + Microphone

## Question

Can ScreenRecorderLib 7.0.1 reliably record one explicitly selected system-audio loopback device and one explicitly selected microphone/capture device together, with acceptable A/V sync and stable device binding?

**Gate status:** NOT RUN

## API under test

The harness uses:

- `Recorder.GetSystemAudioLoopbackDevices()` for render/loopback outputs;
- `Recorder.GetSystemAudioCaptureDevices()` for microphone/capture inputs;
- one `LoopbackAudioSource(loopbackDeviceId)`;
- one `CaptureAudioSource(microphoneDeviceId)`;
- both sources in the same `AudioOptions.AudioSources` collection;
- stereo AAC audio at 128 kbps;
- `OnAudioPacketRecorded` to count mixed packets and distinguish source packets using ScreenRecorderLib source IDs.

Both concrete device IDs are selected before Start. No device follows the Windows default dynamically inside the harness.

## Test matrix

| Case | Setup | Purpose |
| --- | --- | --- |
| C1 | default loopback + default microphone; system playback and speech | baseline simultaneous recording |
| C2 | system-audio-only interval → mic-only interval → both | identify both sources in final audio |
| C3 | explicit non-default microphone, when available | verify concrete microphone binding |
| C4 | change Windows default output during recording | verify loopback session does not silently rebind |
| C5 | change Windows default input during recording | verify microphone session does not silently rebind |
| C6 | unplug/disable selected microphone during recording | observe failure/device-loss behavior |
| C7 | Stop normally and repeat with same two concrete IDs | verify repeatable initialization and finalization |
| C8 | clap/visible transient plus system reference audio | inspect A/V sync and mixed-audio timing in final MP4 |

## Evidence

Each run writes:

- `*.mp4`;
- `*.log` from ScreenRecorderLib;
- `*.g0-3.json` with selected loopback/microphone device IDs, source IDs, enumeration snapshots, recorder states, and packet/byte counts split by source.

Packet callback arrival time is not used as an A/V sync measurement. A/V sync must be checked from the final media using a visible/audible reference event or media-analysis tooling.

## Pass criteria

Mark PASS only if both selected sources are present and usable in the recorded output, concrete device binding is stable, normal Stop finalizes a playable MP4, and measured A/V sync is acceptable for the project baseline.

Use PASS WITH WORKAROUND when a bounded device/configuration workaround produces reliable behavior.

Use FAIL if the two-source audio path cannot satisfy v1 recording reliability.

Summarize reviewed results in `RESULTS.md` and `docs/technical-spike.md`.
