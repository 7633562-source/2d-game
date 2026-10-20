#!/usr/bin/env bash
# BALANCE-ARENA-1 verdict cycle: the two frames that show the new column.
#
# The caption on the frame now carries the controller's own verdict next to the shared
# fall criterion (Assets/ImpactRecorder.cs, ControllerText + OutcomeText). One take per
# side of that pair:
#
#   cv_v1L  circle 0.25 m  0.5 kg   4 m/s  -> torso   he STANDS: neither verdict fires
#   cv_v2L  square 0.30 m 15   kg   4 m/s  -> torso   he goes down: the criterion and
#                                                      the controller both fire, and
#                                                      this cycle measures WHEN each one
#
# `-visual` is a second render on top of the simulation and is not bit-reproducible: the
# numbers to quote are the -nographics runs of Tools/impact-verdict-grid.sh; these takes
# are evidence of the look and of what the frame says.
#
# usage: bash Tools/impact-verdict-video.sh
set -u
cd "E:/Games/2D Game/My project"
RUNS="Trials/Runs"

run() {
  local label="$1"; shift
  echo "=== $label"
  bash Tools/impact-run.sh "$label" 24 --visual "$@" || echo "RUN FAILED $label"
}

run cv_v1L -impact 1 -impactCount 3 -impactSeed 1 -impactShape circle -impactSize 0.25 \
  -impactMass 0.5 -impactSpeed 4 -impactAim torso -shotInterval 0.05
run cv_v2L -impact 1 -impactCount 3 -impactSeed 1 -impactShape square -impactSize 0.30 \
  -impactMass 15 -impactSpeed 4 -impactAim torso -shotInterval 0.05

python F:/hermes-data/scripts/impact-video.py \
  "cv_v1L=circle 0.25 m 0.5 kg 4 m/s -> torso" \
  "cv_v2L=square 0.30 m 15 kg 4 m/s -> torso"

if [ -f "$RUNS/impact_three_throws.mp4" ]; then
  mv -f "$RUNS/impact_three_throws.mp4" "$RUNS/impact_verdict_two_takes.mp4"
  echo "concat cut -> Trials/Runs/impact_verdict_two_takes.mp4"
fi

# Two stills more than impact-video.py makes: the frame at the CONTROLLER's own fall time
# (its ladder is what this cycle wires in, and it fires before the criterion does) and the
# frame after the window closes, where the banner carries the throw's final numbers.
python Tools/impact-verdict-stills.py cv_v1L cv_v2L
echo "VERDICT VIDEO DONE"
