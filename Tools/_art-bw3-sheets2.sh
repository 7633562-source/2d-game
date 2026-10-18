#!/usr/bin/env bash
# ART-BW3 second pass sheets: the same crop box and the same-pose frame from the re-taken film.
# usage: _art-bw3-sheets2.sh <after-frame>
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
FR="${1:?usage: _art-bw3-sheets2.sh <after-frame>}"
M="F:/hermes-data/profiles/h_2dgame_bird/cache/scratch/bw11_mic.py"
R=Trials/Runs/reviews
python "$M" single aBW3b "$FR" 4 "$R/aBW3b_after_mic.png"
python "$M" single aBW3b "$FR" 1 "$R/aBW3b_after_mic1x.png"
python "$M" pair 3 "$R/aBW3b_ab1.png" aBW3pre 92 aBW3b "$FR"
python "$M" pair 3 "$R/aBW3b_ab2.png" aBW3b "$FR" aBW3pre 92
echo "ARTBW3-SHEETS2-DONE"
