#!/usr/bin/env bash
# BALANCE-ARENA-1 - the current-build evidence cycle (level worker, 19.09.2026).
#
# Card `.cursor/tasks/BALANCE-ARENA-1.md`, "Evidence for Yury": a >= 15 s 1080p video of
# the first throws plus >= 3 stills, all under `Trials/Runs`, and the run JSON/CSV with
# the run identity (label, seed, `sourceFingerprint` == `Build/Headless/build-manifest.json`).
#
# Every impact artifact in the folder was produced on an older player, so no run in the
# folder carries the fingerprint of the player the tree is on now. This script re-runs the
# accepted protocol on the CURRENT player (Tools/impact-run.sh stamps the manifest's own
# fingerprint, so a run can never be quoted against a build it did not measure):
#
#   imp_cb_quiet   40 s, `-impactCount 0`, `-nographics`   -> rule zero: the card's four
#                                                            stand numbers must hold
#   imp_cb1/2/3    16 s, one throw, `-nographics`          -> the quotable rows, one per
#                                                            class of the published triple
#   imp_cb1L/2L/3L 24 s, three throws, `-visual`           -> the 1080p takes the video is
#                                                            cut from
#
# `-nographics` only for the numbers (that is the switch set the card's rule zero
# reproduces bit-for-bit); the `-visual` takes are evidence of the LOOK and carry their
# own numbers.
#
# usage: bash Tools/impact-cb-runs.sh [--visual-only]
set -u
cd "E:/Games/2D Game/My project"
RUNS="Trials/Runs"

run() {
  local label="$1" duration="$2"; shift 2
  echo "=== $label"
  bash Tools/impact-run.sh "$label" "$duration" "$@" || echo "RUN FAILED $label"
}

# The three classes of the published triple (front_end_markers 1/2/3).
A1=(-impact 1 -impactSeed 1 -impactShape circle -impactSize 0.25 -impactMass 4 -impactSpeed 8 -impactAim torso)
A2=(-impact 1 -impactSeed 2 -impactShape square -impactSize 0.30 -impactMass 6 -impactSpeed 12 -impactAim head)
A3=(-impact 1 -impactSeed 3 -impactShape rect -impactSize 0.60 -impactMass 12 -impactSpeed 15 -impactAim thigh)

if [ "${1:-}" != "--visual-only" ]; then
  run imp_cb_quiet 40 -impact 1 -impactSeed 1 -impactCount 0
  run imp_cb1 16 -impactCount 1 "${A1[@]}"
  run imp_cb2 16 -impactCount 1 "${A2[@]}"
  run imp_cb3 16 -impactCount 1 "${A3[@]}"
fi

run imp_cb1L 24 --visual -impactCount 3 "${A1[@]}" -shotInterval 0.05
run imp_cb2L 24 --visual -impactCount 3 "${A2[@]}" -shotInterval 0.05
run imp_cb3L 24 --visual -impactCount 3 "${A3[@]}" -shotInterval 0.05

echo "CB-RUNS DONE"
