# Headless stand

From the project root. Unity Editor must be closed only for `-Rebuild`.
Baseline map: [Baselines/README.md](Baselines/README.md).
Documents are English only, always.

```powershell
powershell -Command ".\Tools\run-trial.ps1 -Rebuild -Duration 30 -Label check"
powershell -Command ".\Tools\run-trial.ps1 -Duration 20 -Label pf22 -Extra @('-pushImpulse','22','-pushTime','5')"
powershell -Command ".\Tools\run-trial.ps1 -Duration 20 -Label pb23 -Extra @('-pushImpulse','-23','-pushTime','5')"
```

`+pushImpulse` is forward (+X into the torso), minus is backward. By default
the script passes `-fixedDelta 0.005`. Summaries go to
`Trials/Runs/<label>.json`.

Acceptance canon is `Trials/Baselines/stable-pd/`. Folder `Baselines/current/`
is a historical name and holds `curr_*` with the ostrich knee — not the
target. Old summaries with no fingerprint —
`Trials/Baselines/legacy-no-provenance/`. Do not bulk-delete working runs:
`Runs/README.md`.

`buildManifestPath` and `commandArgs` may contain machine-absolute paths:
that is diagnostics. They are not in the fingerprint and will differ after
a project move.

World frame (not the human stand): `Runs/perf_wave1_lab.md`.
