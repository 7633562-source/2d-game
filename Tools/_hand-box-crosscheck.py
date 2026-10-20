#!/usr/bin/env python3
"""ART-0 27th pass - cross-check: does the fit audit's own PNG decoder agree with PIL?

`Tools/art1-fit-audit.py` measures a part's painted rectangle with a hand-rolled zlib PNG
decoder (`alpha_bbox`), and that number is what the ART-1 "no squashed part" decision was
taken from. This pass measured the same shipped canvas with PIL and got a different box, so
before quoting either number the two readers are put side by side on the SAME bytes:

    python Tools/_hand-box-crosscheck.py <file.png> [<file2.png> ...]
"""

import pathlib
import sys

from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import importlib.util

spec = importlib.util.spec_from_file_location(
    "fit_audit", pathlib.Path(__file__).resolve().parent / "art1-fit-audit.py")
fit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fit)


def main():
    for arg in sys.argv[1:]:
        p = pathlib.Path(arg)
        pil_im = Image.open(p).convert("RGBA")
        pil_box = pil_im.split()[3].point(lambda a: 255 if a > 8 else 0).getbbox()
        audit_box = fit.alpha_bbox(p)
        pil_w = pil_box[2] - pil_box[0]
        pil_h = pil_box[3] - pil_box[1]
        audit_w = audit_box[2] - audit_box[0] if audit_box else 0
        audit_h = audit_box[3] - audit_box[1] if audit_box else 0
        print("%s" % p.name)
        print("  PIL   alpha>8 box %s  ink %dx%d px" % (pil_box, pil_w, pil_h))
        print("  audit alpha>8 box %s  ink %dx%d px" % (audit_box, audit_w, audit_h))
        print("  agree=%s  canvas %dx%d" % (
            (pil_box[0], pil_box[1], pil_w, pil_h) ==
            (audit_box[0], audit_box[1], audit_w, audit_h),
            pil_im.width, pil_im.height))
    return 0


if __name__ == "__main__":
    sys.exit(main())
