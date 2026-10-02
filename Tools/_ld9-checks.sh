#!/usr/bin/env bash
# LEVEL DESIGNER cycle 9 (impact lane): identity, rule zero, reproducibility, drift and the
# CoM-axis cross-check, all read from the runs' own files.
set -u
cd "E:/Games/2D Game/My project"
A="Trials/Runs/impact-pre-82fbb1ec"

echo "################ 1. identity (every label against Build/Headless/build-manifest.json)"
python Tools/impact-cb-table.py 2>&1 | head -40

echo
echo "################ 2. rule zero: the quiet stand against burn_stance.json"
python Tools/impact-json-diff.py Trials/Runs/burn_stance.json Trials/Runs/imp_cb_quiet.json 2>&1 | head -30

echo
echo "################ 3. reproducibility: same seed twice"
md5sum Trials/Runs/imp_cb1.csv Trials/Runs/imp_cb1r.csv
python Tools/impact-json-diff.py Trials/Runs/imp_cb1.json Trials/Runs/imp_cb1r.json 2>&1 | head -12
python Tools/impact-trace-diff.py Trials/Runs/imp_cb1.csv Trials/Runs/imp_cb1r.csv 2>&1 | head -8

echo
echo "################ 4. the delivered rows on THIS player (-nographics)"
python Tools/impact-cb-rows.py 2>&1 | tail -30

echo
echo "################ 5. CoM axes: is the record's pair in the run's OWN trace"
python Tools/impact-com-axes-check.py 2>&1 | tail -40

echo
echo "################ 6. drift vs the 9a47f594 set (impact recorder's own trace, 31 columns)"
for L in imp_cb_quiet imp_cb1 imp_cb2 imp_cb3 imp_cb1L imp_cb2L imp_cb3L; do
  echo "--- $L  row-group"
  python Tools/impact-cb-diverge.py "$A/$L-impacts.csv" "Trials/Runs/$L-impacts.csv" 2>&1 | head -14
  echo "--- $L  the 217-column TrialRecorder trace"
  python Tools/impact-trace-diff.py "$A/$L.csv" "Trials/Runs/$L.csv" 2>&1 | head -8
done

echo
echo "################ 7. the quotable keys, old vs new, key by key"
for L in imp_cb1 imp_cb2 imp_cb3; do
  echo "--- $L"
  python Tools/impact-json-diff.py "$A/$L-impacts.json" "Trials/Runs/$L-impacts.json" 2>&1 | tail -20
done

echo "LD9-CHECKS DONE"
