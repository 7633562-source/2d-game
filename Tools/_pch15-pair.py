# -*- coding: utf-8 -*-
"""B-PERCH-15 (bird lane, 26.09): the lift tie-break sheet - the SAME standard
seat cut (the _pch13-cuts.py 8-panel strip, box 792,224,912,344) for the two
owner takes: top = bPCH14e (half-rise, seat 0.360) / bottom = bPCH15a
(quarter-rise, seat 0.344). Mirrors the bPCH14_a_vs_e sheet geometry
(two strips stacked, gap 12).
Usage: python Tools/_pch15-pair.py
Writes Trials/Runs/reviews/bPCH15_e_vs_a.png
"""
from PIL import Image

REV = r"E:\Games\2D Game\My project\Trials\Runs\reviews"
strip = lambda tag: Image.open(REV + "\\%s_seat.png" % tag).convert("RGB")
top = strip("bPCH14e")
bot = strip("bPCH15a")
print("strips", top.size, bot.size)
gap = 12
out = Image.new("RGB", (max(top.size[0], bot.size[0]),
                        top.size[1] + gap + bot.size[1]), (0, 0, 0))
out.paste(top, (0, 0))
out.paste(bot, (0, top.size[1] + gap))
p = REV + r"\bPCH15_e_vs_a.png"
out.save(p)
print("saved", p, out.size)
