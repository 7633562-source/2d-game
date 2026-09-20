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
  # A build from this script used to leave build-status.txt behind: it writes
  # build-info.json only, so after the 10-minute gate was paused the status file
  # still said "OK at 16:38" at 17:50 while every source under Assets had moved on
  # - the stale-OK trap the gate's own comment names. A successful build proves the
  # player carries every source, so say so here, with the newest source it carries.
  # sort on %T@ (epoch), not on the printed clock: "23:57" of a PREVIOUS day sorts
  # above today's 17:33 in a string sort, so the line reported a source the build
  # never saw. The date goes into the line with the clock.
  newest_src=$(find "$PROJ/Assets" -type f -name '*.cs' -printf '%T@ %TY-%Tm-%Td %TH:%TM\n' 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)
  printf 'Build status: OK at %s. No build action needed (impact-build %s built the player with every source present, newest %s).\n' \
    "$(date +%H:%M)" "$TAG" "${newest_src:-unknown}" > "$TMP/build-status.txt"
  exit 0
else
  echo "TRIAL_BUILD_FAILED exit=$code"
  grep -m5 "error CS\|TRIAL_BUILD_FAILED\|Exception" "$LOG" 2>/dev/null
  # Deliberately NO BROKEN write: a build can also fail on a file lock or the
  # licence service while the tree is fine (the 18.09 09:39 false alarm blocked
  # every slot). BROKEN stays the gate's verdict, taken from a clean rebuild.
  exit 1
fi
exit 0
