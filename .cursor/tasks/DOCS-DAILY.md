# DOCS-DAILY — nightly document check

Standing role, not a wave. Launch: scheduler `Unity-Docs-daily`, 02:00
local time. Script: `Tools/run-docs-daily.ps1`. Do **not** put this on
Cursor cloud Automations: no git, the VM cannot see the folder.

## One sentence

Check pointers and that memory is present, fix meaning, delete nothing.

## Layer

Documents only: map, README, baselines, cards, roster. Do not touch game
code, muscles, PD, or scenes.

## Do

1. Read `DOCS.md`, `CURRENT.md`, `.cursor/rules/docs-map.mdc`.
2. Acceptance canon is `Trials/Baselines/stable-pd/`. Folder
   `Baselines/current/` is history.
3. Check pointers in the root README, `Trials/README.md`, baseline
   READMEs, `CURRENT.md`.
4. Confirm still present: baselines, `Trials/Runs`, `tasks/archive`,
   external CSV `E:\Games\Unity3D LOGS\trials`.
5. A contradiction — append to `DOCS.md`, do not erase the old table.
6. Do not rewrite `current-front.mdc` wholesale.
7. Write the night report to `Trials/Runs/docs_daily/<date>.md` and
   `docs_daily_latest.md`.
8. Short RESULT on the board `F:\Kursor\Admin\inbox\agent-board.md`.
9. Living documents must stay English only (`docs-english.mdc`). Flag
   leftover Cyrillic in `.md` / `.mdc`.

## Forbids

Do not delete or rename `Trials/Baselines/*`, `Trials/Runs/*`,
`tasks/archive/`. Do not start a new baseline. Do not clean Runs. Do not
open a cloud automation for this job. Do not launch `cursor.exe` at 02:00
— that opens the IDE. An agent head only if `agent` or `cursor-agent` is
on PATH. Otherwise the night is a mechanical pointer audit.
