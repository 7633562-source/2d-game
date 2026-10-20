#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ART-H4 instrument: the STILL read - one image in, a terse terse verdict out.

The lane's look-verdict "eyes" (same vendor endpoint as `grok-verdict.py` and the walk lane's
`gemini-trial-verdict.py`, model `google/gemini-flash-latest`).  One comparison per call is the
rule that made tight single crops consistent (art-frame-verification), so this tool sends ONE
image per call and prints the reply whole.

Usage:
  python Tools/art-h4-eye-read.py <image.png> "<question>" [--save Trials/Runs/reviews/<file>.md]
  python Tools/art-h4-eye-read.py <image.png> --knee    # the round's knee question
  python Tools/art-h4-eye-read.py <image.png> --ankle   # the round's ankle question
  python Tools/art-h4-eye-read.py <image.png> --pick    # the A/B pick question

Never prints the API key.  Does not modify the image.
"""
import argparse
import base64
import json
import mimetypes
import os
import re
import sys
import time
import urllib.error
import urllib.request

BASE = "https://api.gpt-chat.by/api/chat/completions"
MODEL = "google/gemini-flash-latest"
ENV_CANDIDATES = [
    r"C:/Users/Admin/AppData/Local/hermes/.env",
    os.path.join(os.environ.get("LOCALAPPDATA", ""), "hermes", ".env"),
]

KNEE_Q = (
    "Это крупный план колена 2D-человечка в момент сильного сгиба (обычная ходьба, вид сбоку). "
    "Оцени, насколько колено читается как ОДНА ЦЕЛЬНАЯ КОНЕЧНОСТЬ, а не как два плоских куска, "
    "наложенных друг на друга. Ответь СТРОГО одной строкой JSON без пояснений и без markdown: "
    '{"score": <1-10>, "limb_read": "<одна короткая фраза: одна конечность | два куска | другое>", '
    '"seam_arc": "<есть ли на стыке дуга/круг/линия, по которой видно наложение: да/нет + одним словом как выглядит>", '
    '"style": "<одна ощутимая проблема, если есть>", "fix_hint": "<одна подсказка>"}'
)
ANKLE_Q = (
    "Это крупный план стопы-голени 2D-человечка в момент шага (вид сбоку). "
    "Оцени, насколько голеностоп читается как ОДНА ЦЕЛЬНАЯ КОНЕЧНОСТЬ, а не как два плоских куска. "
    "Ответь СТРОГО одной строкой JSON без пояснений и без markdown: "
    '{"score": <1-10>, "limb_read": "<одна короткая фраза: одна конечность | два куска | другое>", '
    '"seam_arc": "<есть ли на стыке дуга/круг/линия наложения: да/нет + одним словом как выглядит>", '
    '"style": "<одна ощутимая проблема, если есть>", "fix_hint": "<одна подсказка>"}'
)
LOOK_Q = (
    "Смотришь кадр 2D-игры: крупный план ноги ходящего человечка (вид сбоку, нарисованные текстуры). "
    "Оцени КАЧЕСТВО КАРТИНКИ в целом — насколько это выглядит как цельная нарисованная нога/тело, "
    "а не как набор наложенных плоских деталей. Ответь СТРОГО одной строкой JSON без пояснений и без markdown: "
    '{"look": <1-10>, "worst_artifact": "<один главный видимый артефакт, коротко по-русски>", '
    '"artifact_where": "<где он: в каком месте ноги>", "other_faults": ["до 3 коротких"], "fix_hint": "<одна подсказка>"}'
)
PICK_Q = (
    "Две панели одного и того же места кадра (одинаковое окно съёмки), разделённые розовой полосой: "
    "панель 1 слева, панель 2 справа. Персонаж просто ходит, кадры взяты в похожей фазе шага. "
    "Ответь СТРОГО одной строкой JSON без пояснений и без markdown: "
    '{"pick": "<панель 1 | панель 2>", "why": "<одна короткая причина по-русски: у какой колено/стык читается как одна конечность>"}'
)


def read_key():
    for p in ENV_CANDIDATES:
        try:
            env = open(p, encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        m = [l for l in env.splitlines() if re.match(r"^\s*GPTCHAT_API_KEY\s*=", l)]
        if m:
            return re.sub(r"^\s*GPTCHAT_API_KEY\s*=\s*", "", m[0]).strip().strip('"').strip("'")
    raise SystemExit("no GPTCHAT_API_KEY in .env")


def call(key, path, question, max_tokens=500):
    mime = mimetypes.guess_type(path)[0] or "image/png"
    b64 = base64.b64encode(open(path, "rb").read()).decode()
    body = json.dumps({
        "model": MODEL,
        "messages": [{"role": "user", "content": [
            {"type": "text", "text": question},
            {"type": "image_url", "image_url": {"url": "data:%s;base64,%s" % (mime, b64)}},
        ]}],
        "max_tokens": max_tokens,
    }).encode()
    req = urllib.request.Request(BASE, data=body, headers={
        "Authorization": "Bearer " + key, "Content-Type": "application/json"})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=180) as r:
        d = json.load(r)
    content = ((d.get("choices") or [{}])[0].get("message") or {}).get("content") or ""
    usage = d.get("usage") or {}
    return {"model": d.get("model"), "secs": round(time.time() - t0, 1),
            "usage": usage, "text": content.strip()}


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("image")
    ap.add_argument("question", nargs="?")
    ap.add_argument("--knee", action="store_true")
    ap.add_argument("--ankle", action="store_true")
    ap.add_argument("--look", action="store_true")
    ap.add_argument("--pick", action="store_true")
    ap.add_argument("--save", default="")
    args = ap.parse_args(argv[1:])

    q = args.question
    if args.knee:
        q = KNEE_Q
    elif args.ankle:
        q = ANKLE_Q
    elif args.look:
        q = LOOK_Q
    elif args.pick:
        q = PICK_Q
    if not q:
        print(__doc__)
        return 2
    if not os.path.exists(args.image):
        print("no such file:", args.image)
        return 2

    res = call(read_key(), args.image, q)
    print(res["text"])
    u = res.get("usage") or {}
    print("-- %s | %.1f s | in %s / out %s" % (res["model"], res["secs"],
                                               u.get("prompt_tokens"), u.get("completion_tokens")))
    if args.save:
        with open(args.save, "a", encoding="utf-8", newline="\n") as fh:
            fh.write("\n## %s\n\nQ: %s\n\nA: %s\n" % (os.path.basename(args.image), q, res["text"]))
        print("saved:", args.save.replace("\\", "/"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
