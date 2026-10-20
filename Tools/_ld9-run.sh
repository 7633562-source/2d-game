#!/usr/bin/env bash
# LEVEL DESIGNER cycle 9 (impact lane): re-run the accepted evidence protocol on the
# player on disk, then the repeat that proves reproducibility.
set -u
cd "E:/Games/2D Game/My project"
{
  echo "=== player on disk at $(date +%H:%M:%S)"
  python Tools/_ld9-manifest.py
  echo "=== protocol"
  bash Tools/impact-cb-runs.sh
  echo "=== imp_cb1r (repeat of imp_cb1)"
  bash Tools/impact-run.sh imp_cb1r 16 -impactCount 1 -impact 1 -impactSeed 1 \
    -impactShape circle -impactSize 0.25 -impactMass 4 -impactSpeed 8 -impactAim torso
  echo "=== player after the runs at $(date +%H:%M:%S)"
  python Tools/_ld9-manifest.py
  echo "CB9-RUNS DONE"
} > Logs/level-cb9-runs.txt 2>&1
echo "CB9 SCRIPT EXIT=$?"
