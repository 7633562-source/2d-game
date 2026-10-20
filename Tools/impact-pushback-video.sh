#!/usr/bin/env bash
# BALANCE-ARENA-1 push-back cycle: the two frames Yury needs to SEE the new column.
#
# The caption on the frame now carries the ground channel (Assets/ImpactRecorder.cs,
# GroundText): the passive window (what the blow put on the ground), the measured contact
# duration, and the push-back after it. One take per side of the new column:
#
#   pb_v1L  circle 0.25 m  0.5 kg   4 m/s  -> torso   he STANDS and the leg pushes back
#   pb_v2L  square 0.30 m 15   kg   4 m/s  -> torso   no answer: the ground force never
#                                                      comes back above body weight
#
# `-visual` is a second render on top of the simulation and is not bit-reproducible: the
# numbers to quote are the -nographics runs of Tools/impact-pushback-grid.sh, the videos
# are evidence of the look and of what the frame says.
#
# usage: bash Tools/impact-pushback-video.sh
set -u
cd "E:/Games/2D Game/My project"
RUNS="Trials/Runs"

run() {
  local label="$1"; shift
  echo "=== $label"
  bash Tools/impact-run.sh "$label" 24 --visual "$@" || echo "RUN FAILED $label"
}

run pb_v1L -impact 1 -impactCount 3 -impactSeed 1 -impactShape circle -impactSize 0.25 \
  -impactMass 0.5 -impactSpeed 4 -impactAim torso -shotInterval 0.05
run pb_v2L -impact 1 -impactCount 3 -impactSeed 1 -impactShape square -impactSize 0.30 \
  -impactMass 15 -impactSpeed 4 -impactAim torso -shotInterval 0.05

# impact-video.py rewrites impact_three_throws.mp4 for the concatenated cut; that file is
# the labels cycle's evidence on this tree, so keep this cycle out of its way.
if [ -f "$RUNS/impact_three_throws.mp4" ] && [ ! -f "$RUNS/impact_three_throws_labelled.mp4" ]; then
  cp "$RUNS/impact_three_throws.mp4" "$RUNS/impact_three_throws_labelled.mp4"
  echo "kept the previous cut as impact_three_throws_labelled.mp4"
fi

python F:/hermes-data/scripts/impact-video.py \
  "pb_v1L=circle 0.25 m 0.5 kg 4 m/s -> torso" \
  "pb_v2L=square 0.30 m 15 kg 4 m/s -> torso"

if [ -f "$RUNS/impact_three_throws.mp4" ]; then
  mv -f "$RUNS/impact_three_throws.mp4" "$RUNS/impact_pushback_two_takes.mp4"
  echo "concat cut -> Trials/Runs/impact_pushback_two_takes.mp4"
fi
echo "PUSH-BACK VIDEO DONE"
