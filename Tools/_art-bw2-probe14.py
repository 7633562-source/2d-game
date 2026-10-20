# ART-BW2 probe 14: the tip inset's price - the same rim metric at a few stagger settings.
# Does the tips' own stagger (tipInset) cost the worst notch?
import importlib.util
import sys

sys.path.insert(0, r"E:\Games\2D Game\My project\Tools")
spec = importlib.util.spec_from_file_location(
    "fin", r"E:\Games\2D Game\My project\Tools\_art-bw2-final.py")
fin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fin)

F = fin.FAMILY
print("== the rim metric vs tipInset (w64 primary, w46 secondary) ==")
for tag, cols in (("inset 0/0/0 (v1 tips)", (0.0, 0.0, 0.0)),
                  ("inset 0/6/11", (0.000, 0.006, 0.011)),
                  ("inset 0/12/22 (v2)", (0.000, 0.012, 0.022))):
    fam = [tuple(list(r[:6]) + [cols[i]]) for i, r in enumerate(F)]
    fin.price(tag, fam, fin.W_PRI, fin.W_SEC)
