#!/usr/bin/env bash
# ART-BW3 sheets: the same crop box and the same-pose frames as the round's own instrument
# (bw11_mic.py, x4 single + x3 pair). usage: _art-bw3-sheets.sh <after-frame>
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
FR="${1:?usage: _art-bw3-sheets.sh <after-frame>}"
M="F:/hermes-data/profiles/h_2dgame_bird/cache/scratch/bw11_mic.py"
R=Trials/Runs/reviews
python "$M" single aBW1f2_wander 95 4 "$R/aBW3_before_mic.png"
python "$M" single aBW3pre 92 4 "$R/aBW3_pre_mic.png"
python "$M" single aBW3 "$FR" 4 "$R/aBW3_after_mic.png"
python "$M" pair 3 "$R/aBW3_ab1.png" aBW3pre 92 aBW3 "$FR"
python "$M" pair 3 "$R/aBW3_ab2.png" aBW3 "$FR" aBW3pre 92
python "$M" single aBW3 "$FR" 1 "$R/aBW3_after_mic1x.png"
echo "ARTBW3-SHEETS-DONE"
