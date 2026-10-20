#!/usr/bin/env bash
# ART-BW3 second-pass eyes: the arms that depend on the RE-TAKEN film, same three eyes, same
# questions. The before (aBW1f2 f95) and pre (aBW3pre f92) arms of the first set stand.
# usage: _art-bw3-eyes2.sh <after-frame>
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
FR="${1:?usage: _art-bw3-eyes2.sh <after-frame>}"
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
run "$R/aBW3b_after_mic.png" "$R/aBW3b_after_mic_gemini.md" "$Q/bw11_q_mic.txt" "$G"
run "$R/aBW3b_after_mic.png" "$R/aBW3b_after_mic_grok.md" "$Q/bw11_q_mic.txt" "$K"
run "$R/aBW3b_after_mic.png" "$R/aBW3b_after_mic_haiku.md" "$Q/bw11_q_mic.txt" "$H"
run "$R/aBW3b_after_mic.png" "$R/aBW3b_row_after_gemini.md" "$QR" "$G"
run "$R/aBW3b_after_mic.png" "$R/aBW3b_row_after_grok.md" "$QR" "$K"
run "$R/aBW3b_after_mic.png" "$R/aBW3b_row_after_haiku.md" "$QR" "$H"
run "$R/aBW3b_ab1.png" "$R/aBW3b_ab1_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aBW3b_ab1.png" "$R/aBW3b_ab1_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aBW3b_ab1.png" "$R/aBW3b_ab1_haiku.md" "$Q/bw11_q_ab.txt" "$H"
run "$R/aBW3b_ab2.png" "$R/aBW3b_ab2_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aBW3b_ab2.png" "$R/aBW3b_ab2_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aBW3b_ab2.png" "$R/aBW3b_ab2_haiku.md" "$Q/bw11_q_ab.txt" "$H"
echo "ARTBW3-EYES2-DONE $(date +%H:%M:%S)"
