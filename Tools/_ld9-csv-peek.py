import csv, sys, os
p = r"E:/Games/2D Game/My project/Trials/Runs/imp_cb1-impacts.csv"
with open(p, newline='') as f:
    r = csv.reader(f, delimiter=';')
    head = next(r)
    print("cols", len(head))
    print("has comX", 'comX' in head, "has comY", 'comY' in head, "has comOffset", 'comOffset' in head)
    idx = {n: head.index(n) for n in ('comX','comY') if n in head}
    vals = {n: [] for n in idx}
    rows = 0
    for row in r:
        rows += 1
        for n, i in idx.items():
            try:
                vals[n].append(float(row[i]))
            except Exception:
                vals[n].append(float('nan'))
    print("rows", rows)
    for n, v in vals.items():
        clean = [x for x in v if x == x]
        if clean:
            print(n, "min", min(clean), "max", max(clean), "distinct", len(set(round(x,9) for x in clean)))
        else:
            print(n, "empty/all-NaN")
