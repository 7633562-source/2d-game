#!/usr/bin/env bash
# LEVEL DESIGNER cycle 9 (impact lane): the closing checks - in-plane, record lint, rule zero
# of the untouched reference runs, the banner the log printed vs the banner on the frame, and
# the working tree (this cycle must have touched nothing under Assets/).
set -u
cd "E:/Games/2D Game/My project"

echo "################ in-plane"
python Tools/impact-inplane-check.py 2>&1 | tail -4

echo
echo "################ record lint over the whole folder"
python Tools/impact-json-lint.py 2>&1 | tail -4

echo
echo "################ reference runs untouched (rule zero of the gallery card)"
md5sum Trials/Runs/walk1_load4.json Trials/Runs/dog_art1.json 2>&1

echo
echo "################ the banner the run logged (read from the frames' own log)"
grep -h "IMPACT-BANNER-FINAL" Logs/impact-run-imp_cb1L.log Logs/impact-run-imp_cb2L.log Logs/impact-run-imp_cb3L.log

echo
echo "################ the scene and the harness as they stand"
md5sum Assets/Scenes/BalanceArena.unity Assets/ImpactArena.cs Assets/ImpactThrower.cs Assets/ImpactRecorder.cs Assets/ImpactCaption.cs

echo
echo "################ working tree"
git status --porcelain 2>&1 | head -20
echo "LD9-CHECKS2 DONE"
