#!/usr/bin/env bash
# BALANCE-ARENA-1 - the L2 "no clairvoyance" run set (level worker, 19.09.2026).
#
# Card `.cursor/tasks/BALANCE-ARENA-1.md`, section "No clairvoyance (Yury's rule)", item L2:
#   "the throw time, side and aim vary with the seed, and the human's code must not read the
#    thrower's state at all (grep the arm/torso/leg code for any reference to the projectile
#    and report it). Bracing before the hit is a defect."
#
# L2 had never been measured: the grid always ran seed 1 and nothing had ever compared a
# pre-hit frame against the same frame of the quiet stand, so "he cannot brace" was an
# assumption. This script runs the two comparisons the card asks for, on ONE build:
#
#   1. l2_quiet   -impactCount 0                        (nothing is ever thrown)
#      l2_hit_s1  -impactCount 1 -impactSeed 1          (one throw, seed 1)
#      -> the pre-contact rows of the two runs must be identical: a body that does not know
#         a throw is coming cannot move differently before the blow arrives.
#   2. l2_hit_s1b (a repeat of seed 1), l2_hit_s2, l2_hit_s3
#      -> same seed bit-identical; seeds 1/2/3 must differ in TIME, SIDE and AIM, while the
#         ordered mass*speed stays the same (mass and speed are switches, not seed draws).
#
# One build, `-nographics` only (the switch set the card's rule zero reproduces bit-for-bit).
# The table and the verdicts come from Tools/impact-l2-table.py.
#
# usage: bash Tools/impact-l2-runs.sh
set -u
cd "E:/Games/2D Game/My project"

DUR=12
COMMON=(-impact 1 -impactShape circle -impactSize 0.25 -impactMass 4 -impactSpeed 8 -impactAim torso)

run() {
  local label="$1"; shift
  echo "=== $label"
  bash Tools/impact-run.sh "$label" "$DUR" "$@" || echo "RUN FAILED $label"
}

# 1. rule zero against the same switch shape: nothing thrown vs one throw at 8 s
run l2_quiet  ${COMMON[@]} -impactCount 0
run l2_hit_s1 ${COMMON[@]} -impactCount 1 -impactSeed 1
# 2. the same seed again, then two other seeds
run l2_hit_s1b ${COMMON[@]} -impactCount 1 -impactSeed 1
run l2_hit_s2 ${COMMON[@]} -impactCount 1 -impactSeed 2
run l2_hit_s3 ${COMMON[@]} -impactCount 1 -impactSeed 3

echo "L2 RUNS DONE"
