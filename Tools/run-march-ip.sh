#!/usr/bin/env bash
# March in place: 90° pose on clocked air, ankle cap after 0.08 s, toe/heel after pose-up.
set -euo pipefail
cd "$(dirname "$0")/.."
powershell.exe -NoProfile -Command ".\\Tools\\run-trial.ps1 -Rebuild -Duration 30 -Label march_pin -Extra @('-march','1','-marchTime','5','-marchDuration','24','-marchHold','2.5','-marchPlant','1.5','-marchFirst','left')"
echo MARCH_PIN_DONE
