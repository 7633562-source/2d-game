#!/usr/bin/env python3
"""ART-H4f: the vision route's wallet check - one minimal text-only call.

The dispatcher's note (24.09 12:40-12:52): the standing route answered HTTP 402 (the vendor
account out of balance: 0.37 BYN).  The lane fires this before a round's reads so a dead
wallet is reported as a number, not as a failed read.

Usage: python Tools/arth4f-wallet.py
Prints the HTTP status; on 200 also the reply (expected: a budget line or "pong").
Never prints the key.
"""
import importlib.util
import json
import os
import sys
import urllib.error
import urllib.request

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def eye_tool():
    path = os.path.join(PROJECT, "Tools", "art-h4-eye-read.py")
    spec = importlib.util.spec_from_file_location("art_h4_eye_read", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main():
    m = eye_tool()
    key = m.read_key()
    body = json.dumps({
        "model": m.MODEL,
        "messages": [{"role": "user", "content": "Reply with the single word: pong"}],
        "max_tokens": 8,
    }).encode()
    req = urllib.request.Request(m.BASE, data=body, headers={
        "Authorization": "Bearer " + key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            d = json.load(r)
        txt = ((d.get("choices") or [{}])[0].get("message") or {}).get("content") or ""
        print("HTTP 200 | model=%s | reply=%r" % (d.get("model"), txt.strip()[:60]))
    except urllib.error.HTTPError as e:
        detail = e.read()[:200].decode("utf-8", "replace")
        print("HTTP %s %s | %s" % (e.code, e.reason, detail))
    return 0


if __name__ == "__main__":
    sys.exit(main())
