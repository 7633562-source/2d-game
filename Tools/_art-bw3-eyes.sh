#!/usr/bin/env bash
# ART-BW3 eyes: one session, the standing three eyes (gpt-chat.by route).
# arms: the standing mic question (before aBW1f2 f95 / after aBW3), the row-rhythm question
#       (before / pre aBW3pre / after), and the pair ab1/ab2 (pre vs after, both orders).
# usage: _art-bw3-eyes.sh <after-frame>
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
FR="${1:?usage: _art-bw3-eyes.sh <after-frame>}"
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
run "$R/aBW3_before_mic.png" "$R/aBW3_before_mic_gemini.md" "$Q/bw11_q_mic.txt" "$G"
run "$R/aBW3_before_mic.png" "$R/aBW3_before_mic_grok.md" "$Q/bw11_q_mic.txt" "$K"
run "$R/aBW3_before_mic.png" "$R/aBW3_before_mic_haiku.md" "$Q/bw11_q_mic.txt" "$H"
run "$R/aBW3_after_mic.png" "$R/aBW3_after_mic_gemini.md" "$Q/bw11_q_mic.txt" "$G"
run "$R/aBW3_after_mic.png" "$R/aBW3_after_mic_grok.md" "$Q/bw11_q_mic.txt" "$K"
run "$R/aBW3_after_mic.png" "$R/aBW3_after_mic_haiku.md" "$Q/bw11_q_mic.txt" "$H"
run "$R/aBW3_before_mic.png" "$R/aBW3_row_before_gemini.md" "$QR" "$G"
run "$R/aBW3_before_mic.png" "$R/aBW3_row_before_grok.md" "$QR" "$K"
run "$R/aBW3_before_mic.png" "$R/aBW3_row_before_haiku.md" "$QR" "$H"
run "$R/aBW3_pre_mic.png" "$R/aBW3_row_pre_gemini.md" "$QR" "$G"
run "$R/aBW3_pre_mic.png" "$R/aBW3_row_pre_grok.md" "$QR" "$K"
run "$R/aBW3_pre_mic.png" "$R/aBW3_row_pre_haiku.md" "$QR" "$H"
run "$R/aBW3_after_mic.png" "$R/aBW3_row_after_gemini.md" "$QR" "$G"
run "$R/aBW3_after_mic.png" "$R/aBW3_row_after_grok.md" "$QR" "$K"
run "$R/aBW3_after_mic.png" "$R/aBW3_row_after_haiku.md" "$QR" "$H"
run "$R/aBW3_ab1.png" "$R/aBW3_ab1_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aBW3_ab1.png" "$R/aBW3_ab1_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aBW3_ab1.png" "$R/aBW3_ab1_haiku.md" "$Q/bw11_q_ab.txt" "$H"
run "$R/aBW3_ab2.png" "$R/aBW3_ab2_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aBW3_ab2.png" "$R/aBW3_ab2_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aBW3_ab2.png" "$R/aBW3_ab2_haiku.md" "$Q/bw11_q_ab.txt" "$H"
echo "ARTBW3-EYES-DONE $(date +%H:%M:%S)"
