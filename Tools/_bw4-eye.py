#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""B-WING-4 close: one eye read for the round (temp helper).

Usage: python Tools/_bw4-eye.py <image> <out.md> "<question>" [<model>]
Posts image+question to the standing route (gpt-chat.by), writes the raw answer
plus meta into <out.md>, prints a compact summary. Never prints the key.
"""
import base64
import json
import mimetypes
import os
import re
import sys
import time
import urllib.error
import urllib.request

URL = "https://api.gpt-chat.by/api/chat/completions"
ENV = r"C:\Users\Admin\AppData\Local\hermes\.env"
DEFAULT_MODEL = "google/gemini-flash-latest"


def tok():
    for ln in open(ENV, encoding="utf-8", errors="ignore"):
        m = re.match(r"\s*GPTCHAT_API_KEY\s*=\s*(.+)", ln)
        if m:
            return m.group(1).strip().strip('"').strip("'")
    raise SystemExit("no key")


def main():
    path, out, question = sys.argv[1], sys.argv[2], sys.argv[3]
    model = sys.argv[4] if len(sys.argv) > 4 else DEFAULT_MODEL
    mime = mimetypes.guess_type(path)[0] or "image/png"
    b64 = base64.b64encode(open(path, "rb").read()).decode()
    body = {"model": model, "max_tokens": 1500, "messages": [{"role": "user", "content": [
        {"type": "text", "text": question},
        {"type": "image_url", "image_url": {"url": "data:%s;base64,%s" % (mime, b64)}}]}]}
    req = urllib.request.Request(URL, data=json.dumps(body).encode(),
                                 headers={"Authorization": "Bearer " + tok(),
                                          "Content-Type": "application/json"})
    t0 = time.time()
    try:
        with urllib.request.urlopen(req, timeout=280) as r:
            d = json.load(r)
    except urllib.error.HTTPError as e:
        try:
            eb = e.read().decode("utf-8", "replace")[:400]
        except Exception:
            eb = "?"
        print("HTTP %s %s | %s" % (e.code, e.reason, eb))
        return 1
    secs = time.time() - t0
    txt = ((d.get("choices") or [{}])[0].get("message") or {}).get("content") or ""
    u = d.get("usage", {}) or {}
    md = ["# B-WING-4 eye read: %s" % os.path.basename(path),
          "",
          "Route: gpt-chat.by (%s), asked %s, %.1f s, tokens %s/%s, cost $%s (≈%s BYN)"
          % (d.get("model"), time.strftime("%H:%M"), secs, u.get("prompt_tokens"),
             u.get("completion_tokens"), u.get("cost"), u.get("cost_byn")),
          "",
          "## QUESTION",
          "",
          question,
          "",
          "## ANSWER",
          "",
          txt.strip(),
          ""]
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(md))
    print("read -> %s | %.1f s | $%s | %s" % (out, secs, u.get("cost"),
                                              txt.strip().replace("\n", " ")[:220]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
