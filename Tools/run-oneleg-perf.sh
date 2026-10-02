#!/usr/bin/env bash
# Cold one-leg 20 s hold, both supports. Walk/WalkLift untouched.
set -euo pipefail
cd "$(dirname "$0")/.."
powershell.exe -NoProfile -Command ".\\Tools\\run-trial.ps1 -Rebuild -Duration 30 -Label oneleg_sym -Extra @('-standLeg','right','-standLegTime','5','-standLegHold','20')"
powershell.exe -NoProfile -Command ".\\Tools\\run-trial.ps1 -Duration 30 -Label oneleg_sym_l -Extra @('-standLeg','left','-standLegTime','5','-standLegHold','20')"
echo ONELEG_SYM_DONE
