#!/usr/bin/env bash
# ART-BW3R eyes, arm B: the leg identity. The leg question on the A/B pair (the before cell =
# aL1pre f40, uniform legs; the after = aL1 f40, the far leg's haze) in both orders, on the
# step strip (aL1s, the step switches on), and the standing row question on the after mic.
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
R=Trials/Runs/reviews
QL="F:/hermes-data/profiles/h_2dgame_artist/cache/scratch/artbw3r_q_legs.txt"
QR="F:/hermes-data/profiles/h_2dgame_artist/cache/scratch/artbw3_q_row.txt"
G=google/gemini-flash-latest
K=x-ai/grok-latest
H=anthropic/claude-haiku-latest
run() {
  echo "--- $(basename "$2") $(date +%H:%M:%S)"
  timeout 240 python Tools/_bw4-eye.py "$1" "$2" "$(cat "$3")" "$4" 2>&1 | tail -1
}
run "$R/aL1_legab1.png" "$R/aL1_legab1_gemini.md" "$QL" "$G"
run "$R/aL1_legab1.png" "$R/aL1_legab1_grok.md" "$QL" "$K"
run "$R/aL1_legab1.png" "$R/aL1_legab1_haiku.md" "$QL" "$H"
run "$R/aL1_legab2.png" "$R/aL1_legab2_gemini.md" "$QL" "$G"
run "$R/aL1_legab2.png" "$R/aL1_legab2_grok.md" "$QL" "$K"
run "$R/aL1_legab2.png" "$R/aL1_legab2_haiku.md" "$QL" "$H"
run "$R/aL1_legstep.png" "$R/aL1_legstep_gemini.md" "$QL" "$G"
run "$R/aL1_legstep.png" "$R/aL1_legstep_grok.md" "$QL" "$K"
run "$R/aL1_legstep.png" "$R/aL1_legstep_haiku.md" "$QL" "$H"
run "$R/aL1_after_mic.png" "$R/aL1_row_gemini.md" "$QR" "$G"
run "$R/aL1_after_mic.png" "$R/aL1_row_grok.md" "$QR" "$K"
run "$R/aL1_after_mic.png" "$R/aL1_row_haiku.md" "$QR" "$H"
echo "ARTBW3R-EYES-B-DONE $(date +%H:%M:%S)"
