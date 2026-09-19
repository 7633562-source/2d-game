#!/usr/bin/env python3
"""Gate the impact throw record BEFORE a build is spent on it.

Why this exists
---------------
`Assets/ImpactRecorder.cs` writes its JSON by string concatenation: a field is
`json.Append("\\"key\\":")` followed by the value. A key written as `\\"key\\":\\"`
opens a JSON *string* value and the writer then has to close that quote. On 19.09 a
work-in-progress revision of that writer opened eight numeric fields this way and never
closed them, so the record read `"timeToFallSeconds":"1.5,"peakComExcursionM":...` and
every reader died in `json.load` - including `Tools/impact-pushback-table.py`, the
acceptance tool of the push-back card, and with it the whole cycle's evidence.

The defect is invisible on screen and costs a build plus a trial cycle to discover. This
check is source-only, cheap, and belongs in front of the build.

What it checks
--------------
1. WRITER - every `Append("<literal>")` key opener that leaves a value-string open
             (`:"`) is closed by the writer before the next key. Fatal with line numbers.
             Scans every `Assets/*.cs`, so the same defect in another recorder is caught.
2. WRITER - the CSV header and the per-frame row declare the same number of fields
             (both separated by `Append(';')`). A column added to one writer and not the
             other breaks every CSV reader silently. Fatal.
3. RECORDS- every `Trials/Runs/*.json` parses. Fatal, minus the LEGACY list below - a
             record kept from a writer revision that is no longer in the tree.
4. RECORDS- for the newest records, which writer keys their throw objects lack. Printed
             as a note (an older build legitimately lacks a newer column), never fatal.
5. WRITER - every recorder that declares a CSV header chain AND a per-frame row chain
             has the two agreeing. The check in (2) sees `ImpactRecorder.cs` alone,
             because it locates the row by the method name `AppendRow`; the record every
             walk/arm/dog trial is read from is written by `TrialRecorder.cs` (186
             columns, the row in a chained statement), so a column appended to one side
             of THAT pair was unchecked. Fatal.
6. RECORDS- every stored `Trials/Runs/*.csv` splits into the same number of fields on
             every row as its header does, using the delimiter the header itself uses
             (';' for TrialRecorder, ',' for the capture stream). A comma-only reader
             sees one field per row on the ';' files and reports nothing. Fatal, minus
             the LEGACY_CSV_SHAPE files.

Exit: 0 = OK, 1 = defect (fatal lines printed), 2 = the source could not be read.
"""

import glob
import json
import os
import re
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
IMPACT_SRC = os.path.join(ASSETS, "ImpactRecorder.cs")
RUNS = os.path.join(ROOT, "Trials", "Runs")

# Records written by a writer revision that is gone from the tree. They are kept as
# history, not as evidence: `dog_c38_stand.json` (30.08) carries
# `"lumbarOpenGateDeg":0"useLumbarJointRest":true,,` - a stray quote where a comma
# belongs. The current dog writer emits the field through AppendNum, so it cannot
# reproduce it. Anything NOT in this list makes the gate fail.
LEGACY_UNPARSEABLE = {
    "dog_c38_stand.json": "30.08 stray quote in the lumbar flag writer, revision gone",
}

# Stored CSVs whose header and rows disagree for a reason that is history, not a live
# writer. Same contract as LEGACY_UNPARSEABLE: anything NOT in this list fails the gate.
# `walk_soft_dbg.csv` (28.08, 8001 rows) has a 36-field header and 35-field rows (the
# header even names `dualSoft` twice), so a reader that splits by ';' sees the last
# column missing on every frame - and the writer revision that could emit it is not in
# the tree. The current TrialRecorder pair is checked by csv_shape_all below.
LEGACY_CSV_SHAPE = {
    "walk_soft_dbg.csv": "28.08 debug record, 36-field header over 35-field rows, revision gone",
}

HELPER_KEYS = re.compile(r'\b(?:Num|BoolField|Str|AppendNum|AppendBool)\(\s*json\s*,\s*"([^"]+)"')


def statement_end(src, start):
    """Index of the `;` ending the C# statement that starts at `start` (capped).

    The cap only bites on truncated source: a legitimate chained field can run over dozens
    of `.Append(...)` lines (the `pushDefinition` text is one).
    """
    i, in_string, cap = start, False, min(len(src), start + 4000)
    while i < cap:
        c = src[i]
        if c == "\\":
            i += 2
            continue
        if c == '"':
            in_string = not in_string
        elif c == ";" and not in_string:
            return i
        i += 1
    return i


def csharp_literals(src):
    """Yield (pos, end, decoded) for every `Append("<literal>")` call, by hand.

    A regex over the literal is what missed the defect first time round: eight sites hid
    behind a backslash-quote pair. This walks the literal character by character.
    """
    needle = "Append("
    i = 0
    while True:
        j = src.find(needle, i)
        if j < 0:
            return
        k = j + len(needle)
        while k < len(src) and src[k] in " \t":
            k += 1
        if k < len(src) and src[k] == '"':
            out, closed = [], False
            k += 1
            while k < len(src):
                c = src[k]
                if c == "\\":
                    if k + 1 >= len(src):
                        break
                    out.append({"\\": "\\", '"': '"', "n": "\n", "r": "\r", "t": "\t"}
                               .get(src[k + 1], src[k + 1]))
                    k += 2
                    continue
                if c == '"':
                    closed = True
                    k += 1
                    break
                out.append(c)
                k += 1
            if closed:
                m = k
                while m < len(src) and src[m] in " \t\n\r":
                    m += 1
                if m < len(src) and src[m] == ")":
                    yield j, k, "".join(out)
        i = j + len(needle)


def scan_writers(problems):
    files = sorted(glob.glob(os.path.join(ASSETS, "**", "*.cs"), recursive=True))
    scanned = 0
    for path in files:
        try:
            src = open(path, encoding="utf-8-sig", errors="replace").read()
        except OSError as exc:
            print("CANNOT READ %s: %s" % (path, exc))
            return None
        sites = []
        for pos, end, txt in csharp_literals(src):
            if not (txt.startswith('"') and txt.endswith(':"') and txt.count('"') == 3):
                continue
            sites.append((pos, end, txt[1:-2].rstrip('"')))
        if sites:
            scanned += 1
        ends = [s[1] for s in sites] + [len(src)]
        for idx, (pos, end, key) in enumerate(sites):
            # Two shapes carry a string value and only one of them can be judged from the
            # source alone:
            #   chained  - `json.Append("\"k\":\"").Append(Escape(x)).Append("\",");`
            #   split    - open in one statement, the text in the next, the closer in a
            #              third (`pushDefinition`, lines 1062-1069). The closer of a split
            #              field is a separate statement, so a source check must not judge
            #              it - the record-level check below is what covers that shape.
            # A chained field must close inside its own statement; the 19.09 defect was a
            # chained numeric value with the opening quote never closed.
            j = end + 1
            while j < len(src) and src[j] in " \t\n\r":
                j += 1
            if j >= len(src) or src[j] != ".":
                continue
            seg = src[end:statement_end(src, end)].replace(" ", "")
            if 'Append("\\",")' in seg or 'Append(\'"\')' in seg or 'Append("\\""' in seg:
                continue
            problems.append(
                "WRITER  %s:%d  key %r opens a string value (\"key\":\") and nothing "
                "closes it in its own statement - the record will not parse; the next "
                "value follows as %r"
                % (os.path.relpath(path, ROOT), src.count("\n", 0, pos) + 1, key,
                   src[end:ends[idx + 1]].strip().replace("\n", " ")[:70]))
    print("writer scan: %d .cs files, %d declare record keys" % (len(files), scanned))

    # CSV header vs per-frame row field count. The header is the chained statement whose
    # last line appends a field NAME and a newline; the name is read from the source, not
    # hard-coded, so adding a last column does not turn this check into a false alarm.
    src = open(IMPACT_SRC, encoding="utf-8-sig", errors="replace").read()
    names = list(re.finditer(r'\.Append\("(\w+)"\)\.Append\(\'\\n\'\)', src))
    if not names:
        problems.append("WRITER  no CSV header chain found (a chain ending in "
                        ".Append(\"<name>\").Append('\\n')) - the header shape changed and "
                        "this check cannot see it")
        return 0
    last = names[-1]
    hpos = last.start()
    h_fields = None
    start = src.rfind("\n", 0, hpos) + 1
    while True:
        prev = src.rfind("\n", 0, start - 1) + 1
        if prev == start:
            break
        stripped = src[prev:start].strip()
        # The chain continues while the line opens a builder call (`rows.Append(`) or
        # continues one (`.Append(`). The first line names the builder, so a rule that
        # only accepts `json.Append(` truncates the header by one field.
        if stripped.startswith(".") or re.match(r"^[A-Za-z_][\w.]*\.Append\(", stripped):
            start = prev
            continue
        break
    header = src[start:hpos + len('.Append("%s")' % last.group(1))]
    h_fields = len(re.findall(r"Append\(';'\)", header)) + 1
    row_start = src.find("private void AppendRow(")
    row_end = src.find("\n    }", row_start)
    row = src[row_start:row_end]
    r_fields = len(re.findall(r"Append\(';'\)", row)) + 1
    if h_fields != r_fields:
        problems.append("WRITER  CSV header declares %d fields, the per-frame row writes "
                        "%d - a reader of one is a reader of neither" % (h_fields, r_fields))
    else:
        print("writer CSV shape: header %d fields == row %d fields" % (h_fields, r_fields))
    return h_fields


CHAIN_LINE = re.compile(r"^\s*(?:\.|[A-Za-z_][\w.]*\.Append\()")
CSV_SEP = "Append(';')"


def csv_chains(src):
    """Chained statements that end in `AppendLine()`, as (first, last, fields, names).

    `fields` counts the field separators the statement appends, `names` counts the
    fields written as a literal NAME (`Append("wSup")`). The CSV header is the chain
    of names, the per-frame row is the chain of values and carries none, so the pair
    is identifiable in any recorder instead of only in the file whose row method is
    called `AppendRow`. A comment line inside the statement does not end it - the
    TrialRecorder header carries a comment before each appended block, and a walk-back
    that stops on one measures a fragment of the header, not the header.
    """
    lines = src.splitlines(keepends=True)
    out = []
    for i, line in enumerate(lines):
        if "AppendLine()" not in line:
            continue
        start = i
        while start > 0:
            prev = lines[start - 1].strip()
            if CHAIN_LINE.match(lines[start - 1]) or prev.startswith("//"):
                start -= 1
                continue
            break
        block = [ln for ln in lines[start:i + 1] if CHAIN_LINE.match(ln)]
        fields = sum(ln.count(CSV_SEP) for ln in block) + 1
        names = re.findall(r'\.Append\("(\w+)"\)', "".join(block))
        out.append((start + 1, i + 1, fields, len(names)))
    return out


def csv_shape_all(problems):
    """Check items (5): the header chain and the row chain of every recorder agree."""
    files = sorted(glob.glob(os.path.join(ASSETS, "**", "*.cs"), recursive=True))
    pairs = 0
    for path in files:
        try:
            src = open(path, encoding="utf-8-sig", errors="replace").read()
        except OSError as exc:
            print("CANNOT READ %s: %s" % (path, exc))
            continue
        chains = [c for c in csv_chains(src) if c[2] >= 8]
        named = [c for c in chains if c[3] >= 8]
        valued = [c for c in chains if c[3] == 0]
        if len(named) != 1 or len(valued) != 1:
            continue
        pairs += 1
        head, row = named[0], valued[0]
        rel = os.path.relpath(path, ROOT)
        if head[2] != row[2]:
            problems.append(
                "WRITER  %s: the CSV header chain (from line %d) declares %d fields, "
                "the per-frame row chain (from line %d) writes %d - a reader of one is "
                "a reader of neither" % (rel, head[0], head[2], row[0], row[2]))
        else:
            print("writer CSV shape: %s header %d fields == row %d fields"
                  % (rel, head[2], row[2]))
    if not pairs:
        print("writer CSV shape: no recorder declares a header chain + row chain pair")


def scan_csv_records(problems):
    """Check item (6): header field count vs every data row, per stored CSV.

    The delimiter is read from the header, not assumed: TrialRecorder separates with
    ';' and the capture stream with ','. A comma-only reader sees ONE field per row on
    the ';' files, so it cannot report a defect in them at all - and half the tree is
    ';'. Records are read with utf-8-sig: a BOM is a writer's choice, not a defect.
    """
    if not os.path.isdir(RUNS):
        print("csv records: Trials/Runs is missing")
        return
    names = sorted(os.listdir(RUNS))
    files = scanned = semicolon = ragged = 0
    for name in names:
        if not name.lower().endswith(".csv"):
            continue
        path = os.path.join(RUNS, name)
        try:
            with open(path, "rb") as raw:
                raw.seek(0, os.SEEK_END)
                size = raw.tell()
                complete = True
                if size > 0:
                    raw.seek(size - 1)
                    complete = raw.read(1) in (b"\n", b"\r")
        except OSError as exc:
            problems.append("RECORD  %s unreadable: %s" % (name, exc))
            continue
        try:
            with open(path, encoding="utf-8-sig", errors="replace") as fh:
                header = fh.readline()
                if not header.strip():
                    continue
                delim = ";" if header.count(";") >= header.count(",") else ","
                files += 1
                if delim == ";":
                    semicolon += 1
                want = len(header.rstrip("\r\n").split(delim))
                bad = 0
                first = []
                # A writer that appends one frame at a time can be caught mid-line: a
                # file whose last byte is not a newline has a final line that is not yet
                # a record, and counting it would turn a live trial into a false alarm.
                # A line is judged when the next one arrives; the last one only if the
                # file is terminated.
                prev, prev_no = None, 0
                for i, line in enumerate(fh, start=2):
                    if prev is not None and prev.strip():
                        scanned += 1
                        if len(prev.rstrip("\r\n").split(delim)) != want:
                            bad += 1
                            if len(first) < 5:
                                first.append(prev_no)
                    prev, prev_no = line, i
                if prev is not None and complete and prev.strip():
                    scanned += 1
                    if len(prev.rstrip("\r\n").split(delim)) != want:
                        bad += 1
                        if len(first) < 5:
                            first.append(prev_no)
        except OSError as exc:
            problems.append("RECORD  %s unreadable: %s" % (name, exc))
            continue
        if not bad:
            continue
        ragged += 1
        if name in LEGACY_CSV_SHAPE:
            print("legacy : %s kept as history (%s)" % (name, LEGACY_CSV_SHAPE[name]))
        else:
            problems.append(
                "RECORD  %s: the header declares %d %r-delimited fields and %d of the "
                "rows do not (lines %s) - every field after the difference is read "
                "from the wrong column"
                % (name, want, delim, bad, ",".join(str(x) for x in first)))
    print("csv records: %d files (%d ';'-delimited, the rest ','), %d rows checked, "
          "%d file(s) with a row/header difference"
          % (files, semicolon, scanned, ragged))


def writer_keys():
    keys = set()
    src = open(IMPACT_SRC, encoding="utf-8-sig", errors="replace").read()
    for _, _, txt in csharp_literals(src):
        if txt.startswith('"') and txt.endswith(':"') and txt.count('"') == 3:
            keys.add(txt[1:-2].rstrip('"'))
    keys |= set(HELPER_KEYS.findall(src))
    return keys


def scan_records(problems):
    keys = writer_keys()
    names = sorted(n for n in os.listdir(RUNS)) if os.path.isdir(RUNS) else []
    parsed, bad = 0, 0
    written = []
    bom = []
    for name in names:
        if not name.endswith(".json"):
            continue
        path = os.path.join(RUNS, name)
        try:
            with open(path, "rb") as fh:
                if fh.read(3) == b"\xef\xbb\xbf":
                    bom.append((os.path.getmtime(path), name))
        except OSError:
            pass
        try:
            with open(path, encoding="utf-8-sig", errors="replace") as fh:
                doc = json.load(fh)
        except json.JSONDecodeError as exc:
            bad += 1
            if name in LEGACY_UNPARSEABLE:
                print("legacy : %s kept as history (%s)" % (name, LEGACY_UNPARSEABLE[name]))
            else:
                problems.append("RECORD  %s does not parse: %s (line %d col %d)"
                                % (name, exc.msg, exc.lineno, exc.colno))
            continue
        except OSError as exc:
            problems.append("RECORD  %s unreadable: %s" % (name, exc))
            continue
        parsed += 1
        throws = []
        if isinstance(doc, dict):
            for k in ("throws", "impacts", "windows"):
                if isinstance(doc.get(k), list):
                    throws = doc[k]
                    break
        if throws:
            written.append((os.path.getmtime(path), name, throws))
    print("records: %d parsed, %d unparseable, %d carry throws" % (parsed, bad, len(written)))

    # Which keys of the writer the newest records are missing that some stored record
    # HAS: a column a reader could see before and cannot see now. A note, never a
    # failure - the record in question may legitimately come from an older build.
    seen = set()
    for _, _, throws in written:
        for t in throws:
            if isinstance(t, dict):
                seen |= set(t)
    for _, name, throws in sorted(written)[-3:]:
        have = set()
        for t in throws:
            if isinstance(t, dict):
                have |= set(t)
        lost = sorted(seen - have)
        print("note   : %s carries %d throw keys, missing %d keys a stored record has%s"
              % (name, len(have), len(lost),
                 (": " + ", ".join(lost[:8])) if lost else ""))
    if bom:
        bom.sort(reverse=True)
        newest = time.strftime("%d.%m %H:%M", time.localtime(bom[0][0]))
        print("note   : %d record(s) open with a UTF-8 BOM (newest %s, %s) - a reader "
              "that opens them as plain utf-8 cannot parse them; utf-8-sig (what this "
              "scan uses) hides that" % (len(bom), newest, bom[0][1]))
    else:
        print("note   : no record starts with a UTF-8 BOM")
    return 0


def main():
    problems = []
    if scan_writers(problems) is None:
        return 2
    csv_shape_all(problems)
    scan_csv_records(problems)
    scan_records(problems)
    if problems:
        print("\nBROKEN")
        for p in problems:
            print("  " + p)
        return 1
    print("\nOK - the writer emits balanced JSON, every CSV header matches its rows, "
          "every stored record parses")
    return 0


if __name__ == "__main__":
    sys.exit(main())
