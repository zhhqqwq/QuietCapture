# G0-1 — Area Capture

## Question

Can ScreenRecorderLib 7.0.1 reliably record an arbitrary rectangular area from one selected display to H.264 MP4 using physical-pixel coordinates?

**Gate status:** NOT RUN

## Harness

Run the ScreenRecorderLib spike project on Windows x64. The harness:

- enumerates displays with Recorder.GetDisplays();
- creates one DisplayRecordingSource;
- passes the entered rectangle to DisplayRecordingSource.SourceRect;
- sets the MP4 output frame to the same width and height;
- disables audio so G0-1 isolates video capture;
- uses 30 FPS fixed-framerate H.264 settings;
- writes ScreenRecorderLib debug logs next to the MP4.

The initial hypothesis is that SourceRect coordinates are interpreted in the selected display source coordinate space. The test must verify that assumption rather than treating it as architecture.

## Test matrix

Record the Windows build, GPU/driver, monitor device name, monitor resolution/DPI, rectangle, cursor setting, output dimensions, Stop behavior, and observed result for every case.

| Case | Rectangle | Purpose | Expected evidence |
| --- | --- | --- | --- |
| A1 | 0,0,1280,720 | origin crop | correct top-left area; 1280×720 output |
| A2 | 100,100,640,480 | offset crop | correct offset area; 640×480 output |
| A3 | 101,77,800,600 | odd X/Y with even size | establish whether crop origin has alignment constraints |
| A4 | near selected display bottom-right | boundary crop | no spill into another display; correct output |
| A5 | full selected display using even dimensions | baseline | complete display content |
| A6 | repeat one case with cursor on/off | cursor behavior | cursor follows requested setting |

## Pass criteria

G0-1 can be marked PASS only when all required cases on the target Windows environments produce playable MP4 files with the intended area, expected frame dimensions, no unexplained capture offset, and reliable normal Stop.

Use PASS WITH WORKAROUND if the capability is usable only with a documented coordinate transform, API switch, alignment rule, or other bounded workaround.

Use FAIL if arbitrary single-display area capture cannot be made reliable enough for the v1 interaction model.

## Evidence to retain

For each relevant run retain:

- the MP4 long enough to inspect playback and frame dimensions;
- the ScreenRecorderLib log;
- the exact input parameters;
- screenshots or notes showing the intended rectangle versus captured rectangle;
- any reproduction steps for incorrect offsets, black frames, or Stop failures.

Summarize the evidence in docs/technical-spike.md. Generated media stays out of Git unless a small file is required to reproduce a decision.
