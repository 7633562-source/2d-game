#!/usr/bin/env bash
# ART-BW3R eyes, arm A: the queued wing-strand bar-1 re-read on the bird lane's fresh
# final-state render (bVW1fv_wander f92). The standing questions, the standing three eyes,
# one session. The before cell of the pair is the standing before (aBW1f2 f95).
# usage: _art-bw3r-eyes-a.sh
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
R=Trials/Runs/reviews
Q="F:/hermes-data/profiles/h_2dgame_bird/cache/scratch"
QR="F:/hermes-data/profiles/h_2dgame_artist/cache/scratch/artbw3_q_row.txt"
G=google/gemini-flash-latest
K=x-ai/grok-latest
H=anthropic/claude-haiku-latest
run() {
  echo "--- $(basename "$2") $(date +%H:%M:%S)"
  timeout 240 python Tools/_bw4-eye.py "$1" "$2" "$(cat "$3")" "$4" 2>&1 | tail -2
}
run "$R/aR1_bird_mic.png" "$R/aR1_mic_gemini.md" "$Q/bw11_q_mic.txt" "$G"
run "$R/aR1_bird_mic.png" "$R/aR1_mic_grok.md" "$Q/bw11_q_mic.txt" "$K"
run "$R/aR1_bird_mic.png" "$R/aR1_mic_haiku.md" "$Q/bw11_q_mic.txt" "$H"
run "$R/aR1_bird_mic.png" "$R/aR1_row_gemini.md" "$QR" "$G"
run "$R/aR1_bird_mic.png" "$R/aR1_row_grok.md" "$QR" "$K"
run "$R/aR1_bird_mic.png" "$R/aR1_row_haiku.md" "$QR" "$H"
run "$R/aR1_ab1.png" "$R/aR1_ab1_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aR1_ab1.png" "$R/aR1_ab1_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aR1_ab1.png" "$R/aR1_ab1_haiku.md" "$Q/bw11_q_ab.txt" "$H"
run "$R/aR1_ab2.png" "$R/aR1_ab2_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aR1_ab2.png" "$R/aR1_ab2_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aR1_ab2.png" "$R/aR1_ab2_haiku.md" "$Q/bw11_q_ab.txt" "$H"
echo "ARTBW3R-EYES-A-DONE $(date +%H:%M:%S)"
