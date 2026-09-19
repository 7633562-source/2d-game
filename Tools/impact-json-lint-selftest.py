#!/usr/bin/env python3
"""Self-test for Tools/impact-json-lint.py - a check nobody has seen fail is not a check.

Builds a sandbox (Temp/linttest) holding the real recorders and the real lint, then runs
the lint four times:

  1. clean copy                              - must be OK;
  2. the 19.09 defect reintroduced           - `json.Append("\\"key\\":\\"")` chained onto
     a numeric value (the opening quote is never closed): must be BROKEN and must name
     the line and the key;
  3. one field separator removed from the per-frame row chain of `TrialRecorder.cs`
     - the 186-column record every walk/arm/dog trial is read from: must be BROKEN and
     must name `TrialRecorder.cs`. This is the class the lint used to see only in
     `ImpactRecorder.cs`.
  4. a stored CSV whose rows are short of its header (`a;b;c` / `1;2`): must be BROKEN
     and must name the record. A comma-only reader cannot see this on a ';' record.

Run it whenever the lint changes:  python Tools/impact-json-lint-selftest.py
"""
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SANDBOX = os.path.join(os.environ.get("LOCALAPPDATA", os.environ.get("TEMP", "/tmp")),
                       "Temp", "linttest")

CLEAN = 'json.Append("\\"timeToFallSeconds\\":")'
BROKEN = 'json.Append("\\"timeToFallSeconds\\":\\"")'

SEP = "Append(';')"


def build_sandbox():
    shutil.rmtree(SANDBOX, ignore_errors=True)
    for sub in ("Assets", "Tools", os.path.join("Trials", "Runs")):
        os.makedirs(os.path.join(SANDBOX, sub))
    for name in ("ImpactRecorder.cs", "TrialRecorder.cs"):
        shutil.copy(os.path.join(ROOT, "Assets", name),
                    os.path.join(SANDBOX, "Assets", name))
    lint = os.path.join(SANDBOX, "Tools", "impact-json-lint.py")
    shutil.copy(os.path.join(ROOT, "Tools", "impact-json-lint.py"), lint)
    return lint


def run(lint):
    p = subprocess.run([sys.executable, lint], capture_output=True, text=True)
    return p.returncode, (p.stdout or "") + (p.stderr or "")


def show(title, rc, out):
    print("=== %s: exit %d ===" % (title, rc))
    print("\n".join(l for l in out.splitlines() if l.strip()))


def main():
    lint = build_sandbox()
    writer = os.path.join(SANDBOX, "Assets", "ImpactRecorder.cs")
    trial = os.path.join(SANDBOX, "Assets", "TrialRecorder.cs")

    rc, out = run(lint)
    show("clean writer", rc, out)

    src = open(writer, encoding="utf-8-sig").read()
    if CLEAN not in src:
        print("\nFAIL: the clean opener this test mutates is gone from the writer - "
              "update CLEAN/BROKEN in this file so the self-test keeps testing the real "
              "shape")
        shutil.rmtree(SANDBOX, ignore_errors=True)
        return 1
    open(writer, "w", encoding="utf-8-sig", newline="").write(src.replace(CLEAN, BROKEN, 1))

    rc2, out2 = run(lint)
    show("the 19.09 defect reintroduced", rc2, out2)

    shutil.copy(os.path.join(ROOT, "Assets", "ImpactRecorder.cs"), writer)
    ts = open(trial, encoding="utf-8-sig", newline="").read()
    head_end = ts.find("AppendLine()")
    cut = ts.find(SEP, head_end)
    if head_end < 0 or cut < 0:
        print("\nFAIL: TrialRecorder.cs no longer carries a header chain followed by a "
              "row chain - update this self-test to the new shape")
        shutil.rmtree(SANDBOX, ignore_errors=True)
        return 1
    short = ts[:cut] + ts[cut + len(SEP):]
    open(trial, "w", encoding="utf-8-sig", newline="").write(short)

    rc3, out3 = run(lint)
    show("one row separator removed from TrialRecorder", rc3, out3)

    shutil.copy(os.path.join(ROOT, "Assets", "TrialRecorder.cs"), trial)
    ragged = os.path.join(SANDBOX, "Trials", "Runs", "selftest_ragged.csv")
    with open(ragged, "w", encoding="utf-8", newline="") as fh:
        fh.write("a;b;c\n1;2\n1;2;3\n")

    rc4, out4 = run(lint)
    show("a stored CSV with a short row", rc4, out4)

    # A live trial writes its frames one at a time: a file caught mid-line must not
    # fail the gate, or every running trial raises a false alarm.
    os.remove(ragged)
    partial = os.path.join(SANDBOX, "Trials", "Runs", "selftest_partial.csv")
    with open(partial, "w", encoding="utf-8", newline="") as fh:
        fh.write("a;b;c\n1;2;3\n4;5")          # no trailing newline, last row unfinished

    rc5, out5 = run(lint)
    show("a CSV caught mid-write (unterminated last line)", rc5, out5)

    shutil.rmtree(SANDBOX, ignore_errors=True)

    clean_ok = rc == 0
    caught_json = rc2 == 1 and "WRITER" in out2 and "timeToFallSeconds" in out2
    caught_row = rc3 == 1 and "WRITER" in out3 and "TrialRecorder.cs" in out3
    caught_csv = rc4 == 1 and "RECORD" in out4 and "selftest_ragged.csv" in out4
    partial_ok = rc5 == 0
    print("\nclean writer accepted: %s   JSON defect caught: %s   TrialRecorder row "
          "drift caught: %s   ragged CSV caught: %s   mid-write CSV tolerated: %s"
          % (clean_ok, caught_json, caught_row, caught_csv, partial_ok))
    if clean_ok and caught_json and caught_row and caught_csv and partial_ok:
        print("SELFTEST OK")
        return 0
    print("SELFTEST FAILED")
    return 1


if __name__ == "__main__":
    sys.exit(main())
