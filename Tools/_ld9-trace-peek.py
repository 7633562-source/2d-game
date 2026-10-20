import csv
base = r"E:/Games/2D Game/My project/Trials/Runs/"
for name in ("imp_cb1.csv", "imp_cb_quiet.csv"):
    p = base + name
    with open(p, newline='') as f:
        r = csv.reader(f, delimiter=';')
        head = next(r)
        print("==", name, "cols", len(head))
        want = [n for n in head if any(k in n.lower() for k in ('com', 'pelvis', 'supportm', 'px', 'py'))]
        print("coord-ish cols:", want)
        idx = {n: head.index(n) for n in want}
        vals = {n: [] for n in want}
        rows = 0
        for row in r:
            rows += 1
            for n, i in idx.items():
                try:
                    vals[n].append(float(row[i]))
                except Exception:
                    vals[n].append(float('nan'))
        print("rows", rows, "rowlen-consistency:", len(head))
        for n, v in vals.items():
            c = [x for x in v if x == x]
            if c:
                print("   %-14s min %10.6f max %10.6f distinct %d" % (n, min(c), max(c), len(set(c))))
            else:
                print("   %-14s NO NUMERIC" % n)
