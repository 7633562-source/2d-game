#!/usr/bin/env python3
"""Where the ground force above body weight actually happens (grid runs' own CSVs).

The card wants "whether the rig pushed back at all (vertical force above body weight
after the impact, with the frame number where it happens)". The JSON row gives
`pushTime`, but not WHEN the force peaks, so this script reads each run's own per-frame
CSV (semicolon separated, `time` / `fzTotal` / `contacted` / `projectileDistanceM`) and
reports, per run:

  contact frame, first frame with fz > m*g at or after the contact, the peak fz and how
  long after the contact it happens, the number of frames above weight inside the window
  and the window length, so the above-weight impulse can be read per second and not as
  an artefact of how long the window happened to run.
"""
import csv
import json
import os
import sys

RUNS = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "Trials", "Runs")
WEIGHT_N = 694.9403          # bodyWeightN in the run JSONs (70.84 kg stand)


def main():
    labels = sys.argv[1:]
    print("| label | contact s | first fz>W s | +s | peak fz N | peak fz/W | peak at +s | "
          "window s | frames above W | above-W impulse N*s | N*s per s above |")
    for label in labels:
        path = os.path.join(RUNS, label + "-impacts.csv")
        rows = []
        with open(path, newline="", encoding="utf-8-sig") as handle:
            for row in csv.DictReader(handle, delimiter=";"):
                rows.append(row)
        contact = None
        for row in rows:
            if row["contacted"] == "1":
                contact = float(row["time"])
                break
        if contact is None:
            print(f"| {label} | no contact |")
            continue
        # The recorder closes the window windowSeconds after the contact, so the
        # above-weight numbers below are the window's, not the whole run's.
        json_path = os.path.join(RUNS, label + "-impacts.json")
        with open(json_path, encoding="utf-8-sig") as handle:
            doc = json.load(handle)
        window_seconds = float(doc.get("windowSeconds", 4.5))
        post = [row for row in rows
                if contact <= float(row["time"]) <= contact + window_seconds]
        window = len(post) * 0.005
        above = [row for row in post if float(row["fzTotal"]) > WEIGHT_N]
        first = above[0] if above else None
        peak = max(post, key=lambda r: float(r["fzTotal"]))
        impulse = sum((float(r["fzTotal"]) - WEIGHT_N) * 0.005 for r in above)
        print("| {label} | {contact:.3f} | {first_t} | {delta} | {peak_fz:.1f} | "
              "{peak_ratio:.3f} | {peak_at} | {window:.3f} | {frames} | {imp:.3f} | "
              "{rate} |".format(
                  label=label, contact=contact,
                  first_t=f"{float(first['time']):.3f}" if first else "-",
                  delta=f"{float(first['time']) - contact:+.3f}" if first else "-",
                  peak_fz=float(peak["fzTotal"]),
                  peak_ratio=float(peak["fzTotal"]) / WEIGHT_N,
                  peak_at=f"{float(peak['time']) - contact:+.3f}",
                  window=window, frames=len(above), imp=impulse,
                  rate=f"{impulse / window:.2f}" if window > 0 else "-"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
