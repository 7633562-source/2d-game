#!/usr/bin/env bash
# ART-BW1 eyes: one session, the standing three eyes (gpt-chat.by route).
# arms: before micro x3, after micro x3, pair ab1 x3, pair ab2 x3 (the standing questions);
#       the targeted tone question on before x3 and after x3.
set -uo pipefail
cd "/e/Games/2D Game/My project" || exit 1
R=Trials/Runs/reviews
Q="F:/hermes-data/profiles/h_2dgame_bird/cache/scratch"
QT="F:/hermes-data/profiles/h_2dgame_artist/cache/scratch/artbw1_q_tone.txt"
G=google/gemini-flash-latest
K=x-ai/grok-latest
H=anthropic/claude-haiku-latest
run() {
  echo "--- $(basename "$2") $(date +%H:%M:%S)"
  timeout 240 python Tools/_bw4-eye.py "$1" "$2" "$(cat "$3")" "$4" 2>&1 | tail -3
}
run "$R/aBW1_before_mic.png" "$R/aBW1_before_mic_gemini.md" "$Q/bw11_q_mic.txt" "$G"
run "$R/aBW1_before_mic.png" "$R/aBW1_before_mic_grok.md" "$Q/bw11_q_mic.txt" "$K"
run "$R/aBW1_before_mic.png" "$R/aBW1_before_mic_haiku.md" "$Q/bw11_q_mic.txt" "$H"
run "$R/aBW1_after_mic.png" "$R/aBW1_after_mic_gemini.md" "$Q/bw11_q_mic.txt" "$G"
run "$R/aBW1_after_mic.png" "$R/aBW1_after_mic_grok.md" "$Q/bw11_q_mic.txt" "$K"
run "$R/aBW1_after_mic.png" "$R/aBW1_after_mic_haiku.md" "$Q/bw11_q_mic.txt" "$H"
run "$R/aBW1_ab1.png" "$R/aBW1_ab1_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aBW1_ab1.png" "$R/aBW1_ab1_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aBW1_ab1.png" "$R/aBW1_ab1_haiku.md" "$Q/bw11_q_ab.txt" "$H"
run "$R/aBW1_ab2.png" "$R/aBW1_ab2_gemini.md" "$Q/bw11_q_ab.txt" "$G"
run "$R/aBW1_ab2.png" "$R/aBW1_ab2_grok.md" "$Q/bw11_q_ab.txt" "$K"
run "$R/aBW1_ab2.png" "$R/aBW1_ab2_haiku.md" "$Q/bw11_q_ab.txt" "$H"
run "$R/aBW1_before_mic.png" "$R/aBW1_tone_before_gemini.md" "$QT" "$G"
run "$R/aBW1_before_mic.png" "$R/aBW1_tone_before_grok.md" "$QT" "$K"
run "$R/aBW1_before_mic.png" "$R/aBW1_tone_before_haiku.md" "$QT" "$H"
run "$R/aBW1_after_mic.png" "$R/aBW1_tone_after_gemini.md" "$QT" "$G"
run "$R/aBW1_after_mic.png" "$R/aBW1_tone_after_grok.md" "$QT" "$K"
run "$R/aBW1_after_mic.png" "$R/aBW1_tone_after_haiku.md" "$QT" "$H"
echo "ARTBW1-EYES-DONE $(date +%H:%M:%S)"
