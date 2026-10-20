#!/usr/bin/env python
"""B-WING-4 close: probe the gpt-chat.by route's state (temp helper; no secrets printed).

Usage: python Tools/_bw4-eye-probe.py <image> [<image> ...]
Posts tiny text calls (mimo, grok) and one image call; prints status + body head.
"""
import base64
import json
import re
import sys
import urllib.error
import urllib.request

URL = "https://api.gpt-chat.by/api/chat/completions"
ENV = r"C:\Users\Admin\AppData\Local\hermes\.env"


def tok():
    for ln in open(ENV, encoding="utf-8", errors="ignore"):
        m = re.match(r"\s*GPTCHAT_API_KEY\s*=\s*(.+)", ln)
        if m:
            return m.group(1).strip().strip('"').strip("'")
    raise SystemExit("no key")


def post(body, key):
    req = urllib.request.Request(URL, data=json.dumps(body).encode(),
                                 headers={"Authorization": "Bearer " + key,
                                          "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            d = json.load(r)
        u = d.get("usage", {}) or {}
        txt = ((d.get("choices") or [{}])[0].get("message") or {}).get("content") or ""
        return "OK model=%s cost=%s byn=%s text=%r" % (
            d.get("model"), u.get("cost"), u.get("cost_byn"), txt[:120])
    except urllib.error.HTTPError as e:
        try:
            body = e.read().decode("utf-8", "replace")[:300]
        except Exception:
            body = "?"
        return "HTTP %s %s | %s" % (e.code, e.reason, body)
    except Exception as e:
        return "ERR %s" % e


def main():
    key = tok()
    for model in ("mimo/latest", "mimo/mimo-flash", "x-ai/grok-latest"):
        body = {"model": model, "max_tokens": 30,
                "messages": [{"role": "user", "content": "Ответь одним словом: тест"}]}
        print("[text] %s -> %s" % (model, post(body, key)))
    for path in sys.argv[1:]:
        b64 = base64.b64encode(open(path, "rb").read()).decode()
        body = {"model": "mimo/latest", "max_tokens": 120,
                "messages": [{"role": "user", "content": [
                    {"type": "text", "text": "Что видно на кадре? Одно предложение."},
                    {"type": "image_url",
                     "image_url": {"url": "data:image/png;base64," + b64}}]}]}
        print("[img:%s] -> %s" % (path, post(body, key)))


if __name__ == "__main__":
    main()
