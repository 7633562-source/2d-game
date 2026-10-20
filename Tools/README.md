# Object scripts

Project root = the folder that contains `run-trial.ps1`.
Documents are English only, always.

| Script | Why |
|---|---|
| `run-trial.ps1` | human headless stand; protocol in `trial-protocol.mdc` |
| `TrialEnv.ps1` | Unity / build / summary paths |
| `run-world-perf.ps1` | World frame measure (`-Graphics -LevelProfile flat\|test1`) |
| `run-perf-audit.ps1` | budget audit |
| `run-gpu10-hourly.ps1` | hourly GPU% (not cloud) |
| `run-docs-daily.ps1` | nightly document check, 02:00, task `Unity-Docs-daily` |
| `close-unity.ps1` | close the Editor before `-Rebuild` |
| `sweep-muscle.ps1` | muscle sweep |
| `gen-art.ps1` | utility art |
| `preview-human.py` / `PreviewHuman/` | human preview outside Play |

Yard texture brief: `visul-test1-textures.md`.
Grok Bot roster (not Unity): `grok-bot-roster.txt`.
