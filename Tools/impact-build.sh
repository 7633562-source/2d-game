#!/usr/bin/env bash
# BALANCE-ARENA-1 build, from bash (the card's rule: builds from bash only).
#
#   bash Tools/impact-build.sh [tag]
#
# Mirrors F:/hermes-data/scripts/grok-trial-run.sh: the Unity editor detaches from a
# PowerShell parent and the exit code is lost, so the build runs here where the wait is
# real. It takes the same build lock the compile gate uses, refuses to start while a
# Unity EDITOR holds the project (the Hub does not count), and prints TRIAL_BUILD_OK /
# TRIAL_BUILD_FAILED so the caller can tell a build from a skipped one.
set -uo pipefail

PROJ="E:/Games/2D Game/My project"
UNITY="F:/6000.5.8f1/Editor/Unity.exe"
TMP="$LOCALAPPDATA/Temp/grok-burn"
TAG="${1:-impact-build}"
LOG="$TMP/$TAG.log"
LOCKD="$TMP/build.lock"

mkdir -p "$TMP"

# Only a real editor blocks the build: `Get-Process Unity` also matches Unity Hub.
editors=$(tasklist /FO CSV /NH 2>/dev/null | tr -d '\000' | grep -ac "Unity.exe" || true)
if [ "${editors:-0}" != "0" ]; then
  echo "TRIAL_BUILD_SKIPPED: a Unity editor is running"
  exit 3
fi

# Serialise with the compile gate and the other streams: mkdir is atomic.
GOT=0
for _ in $(seq 1 40); do
  if mkdir "$LOCKD" 2>/dev/null; then GOT=1; break; fi
  echo "waiting for the build lock"
  sleep 15
done
if [ "$GOT" != "1" ]; then
  echo "TRIAL_BUILD_SKIPPED: the build lock is held"
  exit 3
fi
trap 'rmdir "$LOCKD" 2>/dev/null' EXIT

rm -f "$LOG"
"$UNITY" -batchmode -nographics -projectPath "$PROJ" \
  -executeMethod HeadlessBuild.BuildTrialPlayer -buildOut "Build/Headless" \
  -logFile "$LOG" -quit >> "$TMP/$TAG-console.log" 2>&1
code=$?

if grep -q "TRIAL_BUILD_OK" "$LOG" 2>/dev/null; then
  echo "TRIAL_BUILD_OK"
else
  echo "TRIAL_BUILD_FAILED exit=$code"
  grep -m5 "error CS\|TRIAL_BUILD_FAILED\|Exception" "$LOG" 2>/dev/null
  exit 1
fi
exit 0
