# Play Capture

`PlayCaptureRecorder` runs in non-headless Play mode and stores PNG frames in:

- `Trials/Captures/<utc>_<scene>_<reason>/frame_XXXX_tTTTT.TTT.png`
- `Trials/Captures/<utc>_<scene>_<reason>/meta.txt`

By default the recorder saves a zoomed crop around the player (`zoomHumanInCapture = true`),
so the human is larger on screenshots.

How capture starts/stops:

- Create/remove `Tools/force-play-capture.flag` (armed while file exists).
- Press `F8` in Play Mode to toggle.
- Auto-start once on first walk input (`MotionIntent.moveX` + `StepPhaseDriver.walkActive`).

Helper export:

- `powershell -ExecutionPolicy Bypass -File .\Tools\export-play-capture.ps1 -Zip`
- Optional MP4 (if `ffmpeg` is in PATH): add `-Video`
