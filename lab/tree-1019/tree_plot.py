"""tree-1019 picture -> D:/hermes-data/claude-lab/frames/tree-1019/tree.png (Russian labels)."""
import json, math, os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.gridspec import GridSpec
from tree_core import *
from tree_designs import *

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = r"D:/hermes-data/claude-lab/frames/tree-1019/tree.png"
R = json.load(open(os.path.join(HERE, "results.json"), encoding="utf-8"))
A = np.load(os.path.join(HERE, "proto_arrays.npz"))
E_P = 31.0
seg = build_seg(E_P, rule="pair", amax=0.5)
p18 = build_p18(E_P)

plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 9, "axes.titlesize": 10.5, "axes.labelsize": 9,
                     "xtick.labelsize": 8, "ytick.labelsize": 8, "legend.fontsize": 7.8})
C_ORD = {0: "#6b4a2b", 1: "#9a6a3a", 2: "#4f8a3a", 3: "#86b84f"}
fig = plt.figure(figsize=(18.5, 10.6), dpi=105)
gs = GridSpec(2, 4, figure=fig, left=0.045, right=0.985, top=0.885, bottom=0.115, wspace=0.34, hspace=0.50,
              width_ratios=[1.0, 1.05, 1.05, 1.0])
fig.suptitle("Деревья 19.10: сегментное дерево с листвой — ветер, частоты, посадка и взлёт вороны, устойчивость при шаге 0,005 с",
             fontsize=13.5, fontweight="bold", y=0.975)
fig.text(0.5, 0.932, "Модель: плоская цепь звеньев (геометрия и массы из сборки игры), шаг Time.fixedDeltaTime = 0,005 с. "
         "P18 = ожидаемое дерево req-287 (18 звеньев); SEG24 = предложение (сук и ветвь по 2 сужающихся звена, листва распределена).",
         ha="center", fontsize=9.6, color="#333333")

# ---------- A: strobe silhouettes ----------
ax = fig.add_subplot(gs[0, 0])
tg = A["gust_t_SEG24"]; qg = A["gust_q_SEG24"]; ug = A["gust_u_SEG24"]
tpk = R["frames"]["t_peak"]
sel = [int(np.argmin(np.abs(tg - (tpk + d)))) for d in np.linspace(-2.4, 0.6, 7)]
for k, i in enumerate(sel):
    ph, base, e = seg.kin(qg[i])
    alpha = 0.18 + 0.82 * (k + 1) / len(sel)
    last = (k == len(sel) - 1)
    for b in range(seg.n):
        x0, y0 = base[b]; x1, y1 = base[b] + e[b]
        lw = max(1.0, seg.W[b] * 95)
        ax.plot([x0, x1], [y0, y1], color=C_ORD[seg.ORD[b]] if last else "#7a7a7a", lw=lw if last else lw * 0.55,
                alpha=1.0 if last else alpha * 0.55, solid_capstyle="round", zorder=3 if last else 2)
    if last:
        fp = np.array([base[p[0]] + p[1] * e[p[0]] for p in seg.pts])
        ax.scatter(fp[:, 0], fp[:, 1], s=[p[2] * 2600 for p in seg.pts], color="#3f8f3a", alpha=0.55, zorder=4,
                   edgecolors="none")
ax.plot([-1.1, 1.1], [0.0, 0.0], color="#8a7a5a", lw=2)
ax.add_patch(plt.Rectangle((-0.09, 0.0), 0.18, 0.22, color="#5a3d22", zorder=2))
u_now = float(ug[sel[-1]])
ax.annotate("", xy=(-0.55, 2.22), xytext=(-1.0, 2.22), arrowprops=dict(arrowstyle="-|>", color="#2f5f9f", lw=2))
ax.text(-1.0, 2.29, "ветер %.1f м/с" % u_now, color="#2f5f9f", fontsize=8.5)
ax.set_xlim(-1.1, 1.1); ax.set_ylim(-0.05, 2.42); ax.set_aspect("equal"); ax.set_xticks([-1, -0.5, 0, 0.5, 1])
ax.set_title("Кадры качания в порыв (SEG24), шаг 0,5 с\nсерые — прошлые кадры, цветной — последний", fontsize=10)
ax.set_xlabel("x, м"); ax.set_ylabel("высота, м")
ax.text(0.98, 0.02, "листва: точки массы\n(без отдельных тел)", transform=ax.transAxes, ha="right", va="bottom",
        fontsize=7.8, color="#3f8f3a")

# ---------- B: wind and crown tip ----------
ax = fig.add_subplot(gs[0, 1:3])
crown = int(np.argmax(seg.rest[1][:, 1] + seg.rest[2][:, 1]))
tipx = np.array([seg.kin(q)[1][crown][0] + seg.kin(q)[2][crown][0] for q in qg]) * 100
tipx -= tipx[0]
tp = A["gust_t_P18"]; qp = A["gust_q_P18"]
crown_p = int(np.argmax(p18.rest[1][:, 1] + p18.rest[2][:, 1]))
tipp = np.array([p18.kin(q)[1][crown_p][0] + p18.kin(q)[2][crown_p][0] for q in qp]) * 100
tipp -= tipp[0]
l1, = ax.plot(tp, tipp, color="#b07a3a", lw=1.0, alpha=0.85, label="верхушка P18 (req-287), см")
l2, = ax.plot(tg, tipx, color="#3f8f3a", lw=1.3, label="верхушка SEG24, см")
ax.axvspan(tg[sel[0]], tg[sel[-1]], color="#dddddd", alpha=0.6, zorder=0)
ax.text(tg[sel[0]], ax.get_ylim()[1] if False else 0, "", fontsize=1)
ax.set_xlabel("время, с"); ax.set_ylabel("отклонение верхушки, см")
ax.set_xlim(0, 60)
ax2 = ax.twinx()
l3, = ax2.plot(tg, ug, color="#2f5f9f", lw=0.8, alpha=0.55, label="ветер у кроны, м/с (OU req-287: 2,6 ± 1,4)")
ax2.set_ylabel("ветер, м/с", color="#2f5f9f"); ax2.tick_params(axis="y", colors="#2f5f9f")
ax2.set_ylim(0, max(ug) * 2.1)
lo_, hi_ = min(tipx.min(), tipp.min()), max(tipx.max(), tipp.max())
ax.set_ylim(lo_ - 0.9 * (hi_ - lo_), hi_ + 0.15 * (hi_ - lo_))
ax.legend(handles=[l3, l1, l2], loc="upper center", bbox_to_anchor=(0.5, -0.13), ncol=3, frameon=False)
e3 = R["E3"]
pk_p = np.mean([e3["P18_s%d" % s]["crown_pkpk_cm"] for s in (1, 2, 3)])
pk_s = np.mean([e3["SEG24_s%d" % s]["crown_pkpk_cm"] for s in (1, 2, 3)])
bo_p = np.mean([e3["P18_s%d" % s]["bole_pkpk_mm"] for s in (1, 2, 3)])
bo_s = np.mean([e3["SEG24_s%d" % s]["bole_pkpk_mm"] for s in (1, 2, 3)])
ax.set_title("Ветер 60 с и отклонение верхушки (3 случайных ветра: размах верхушки P18 %.0f см, SEG24 %.0f см; "
             "верх ствола %.0f / %.0f мм)" % (pk_p, pk_s, bo_p, bo_s), fontsize=10)
ax.text(tg[sel[0]] - 0.3, ax.get_ylim()[1] - 0.06 * (ax.get_ylim()[1] - ax.get_ylim()[0]), "кадры слева",
        ha="right", fontsize=7.8, color="#555555")

# ---------- C: spectra ----------
ax = fig.add_subplot(gs[0, 3])
f = A["psd_f"]
m = (f > 0.04) & (f < 12)
ax.axvspan(0.2, 1.0, color="#e8d9c4", alpha=0.7, zorder=0)
ax.axvspan(1.0, 5.0, color="#dcebd2", alpha=0.7, zorder=0)
ax.text(0.45, 9.0, "ствол\n0,2–1 Гц", ha="center", fontsize=7.8, color="#6b4a2b")
ax.text(2.2, 9.0, "ветви\nбыстрее", ha="center", fontsize=7.8, color="#3f6f2a")
for key, lab, col, ls in (("bole", "верх ствола", C_ORD[0], "-"), ("limb_q", "сук (угол)", C_ORD[1], "-"),
                          ("branch_q", "ветвь (угол)", C_ORD[2], "-"), ("twig_q", "веточка (угол)", C_ORD[3], "-")):
    P = A["psd_SEG24_%s" % key][m]
    ax.loglog(f[m], P / P.max(), color=col, lw=1.3, ls=ls, label=lab)
Pp = A["psd_P18_bole"][m]
ax.loglog(f[m], Pp / Pp.max(), color="#b07a3a", lw=1.0, ls="--", label="верх ствола P18")
Pw = A["psd_SEG24_wind"][m]
ax.loglog(f[m], Pw / Pw.max(), color="#2f5f9f", lw=0.9, ls=":", label="ветер")
md = []
for mm in md[:12]:
    ax.plot([mm["f"], mm["f"]], [3e-7, 1.5e-6], color=C_ORD[mm["order"]], lw=1.6)

ax.set_ylim(1e-7, 80.0); ax.set_xlim(0.04, 12)
ax.set_xlabel("частота, Гц"); ax.set_ylabel("спектр (норм. к максимуму)")
ax.set_title("Спектры качания SEG24 в ветре\n(пик у всех — мода ствола 0,30 Гц)", fontsize=10)
ax.legend(loc="lower left", frameon=True, framealpha=0.9, ncol=1)

# ---------- D, E: crow ----------
tc = A["crow_t"]
curves = [("P18_step", "P18 сейчас: вес ступенькой", "#b07a3a", "--", 1.2),
          ("SEG24_step", "SEG24: вес ступенькой", "#8c8c8c", "-", 1.1),
          ("SEG24_kicks", "SEG24: толчок посадки + отталкивание (предлагаемое)", "#2a7a2a", "-", 1.8),
          ("SEG24_kickmass", "эталон: толчок + масса птицы на ветке", "#111111", ":", 1.5)]
for col_i, (t_lo, t_hi, title, mark, mlab) in enumerate(((0.3, 2.6, "Посадка вороны на веточку (0,40–0,52 кг)", T_L := 0.5, "посадка"),
                                                         (6.2, 8.6, "Взлёт вороны с той же веточки", 6.5, "взлёт"))):
    ax = fig.add_subplot(gs[1, col_i])
    mm_ = (tc >= t_lo) & (tc <= t_hi)
    for key, lab, col, ls, lw in curves:
        y = A["crow_y_%s_twig" % key] * 100
        ax.plot(tc[mm_], y[mm_], color=col, ls=ls, lw=lw, label=lab)
    ax.axvline(mark, color="#aa3333", lw=1, ls="-.")
    yl = ax.get_ylim()
    ax.text(mark + 0.04, yl[0] + 0.05 * (yl[1] - yl[0]), mlab, color="#aa3333", fontsize=8.5)
    ax.set_xlabel("время, с"); ax.set_ylabel("высота точки посадки, см")
    ax.set_title(title, fontsize=10)
    ax.grid(alpha=0.25)
    if col_i == 0:
        fig.legend(handles=ax.get_lines()[:4], loc="lower center", bbox_to_anchor=(0.27, 0.0), ncol=2,
                   frameon=False, fontsize=8)
    else:
        ax.set_ylim(-4.1, 3.4)
e4 = R["E4"]
txt = ("8 случайных посадок на веточки, средние, см:\n"
       "за 0,3 с после посадки: ступ. %.1f · толчок %.1f · эталон %.1f\n"
       "при взлёте: ступ. %.1f · толчок %.1f · эталон %.1f" % (
           e4["SEG24_step"]["twig"]["dip_fast_cm"]["mean"], e4["SEG24_kicks"]["twig"]["dip_fast_cm"]["mean"],
           e4["SEG24_kickmass"]["twig"]["dip_fast_cm"]["mean"],
           max(0.0, e4["SEG24_step"]["twig"]["takeoff_dip_cm"]["mean"]), e4["SEG24_kicks"]["twig"]["takeoff_dip_cm"]["mean"],
           e4["SEG24_kickmass"]["twig"]["takeoff_dip_cm"]["mean"]))
txt = txt.replace(".", ",")
fig.axes[-1].text(0.02, 0.97, txt, transform=fig.axes[-1].transAxes, fontsize=7.4, va="top",
                  bbox=dict(boxstyle="round,pad=0.3", fc="white", ec="#bbbbbb", alpha=0.95))

# ---------- F: stability ----------
ax = fig.add_subplot(gs[1, 2])
rules = [("true", "истинная\nинерция"), ("r12", "r12: только\nствол"), ("pair1", "все пары\na ≤ 1"),
         ("pair0.5", "все пары\na ≤ 0,5"), ("pair0.25", "все пары\na ≤ 0,25")]
cols = [("P18", 1), ("P18", 2), ("P18", 4), ("SEG24", 1), ("SEG24", 2), ("SEG24", 4)]
grid = np.zeros((len(rules), len(cols)))
for i, (rk, _) in enumerate(rules):
    for j, (d, km) in enumerate(cols):
        grid[i, j] = 0 if R["E2"][d][rk]["runs"]["game_k%d" % km]["unstable"] else 1
from matplotlib.colors import ListedColormap
ax.imshow(grid, cmap=ListedColormap(["#e8a0a0", "#a8d8a0"]), aspect="auto", vmin=0, vmax=1)
for i, (rk, _) in enumerate(rules):
    for j, (d, km) in enumerate(cols):
        ax.text(j, i, "держит" if grid[i, j] else "взрыв", ha="center", va="center", fontsize=7.6)
ax.set_xticks(range(len(cols)))
ax.set_xticklabels(["k×%d" % km for _, km in cols])
ax.set_yticks(range(len(rules)))
ax.set_yticklabels([lab for _, lab in rules], fontsize=7.6)
ax.axvline(2.5, color="white", lw=4)
ax.text(1.0, -0.85, "P18 (req-287)", ha="center", fontsize=8.5, fontweight="bold")
ax.text(4.0, -0.85, "SEG24 (предложение)", ha="center", fontsize=8.5, fontweight="bold")
ax.set_ylim(len(rules) - 0.5, -1.25)
ax.set_title("Устойчивость при шаге 0,005 с (решатель как Box2D, демпфер игры)\n"
             "строки — правило подъёма инерции; k×2, k×4 — запас",
             fontsize=10)
ax.tick_params(axis="both", length=0)
for sp in ax.spines.values():
    sp.set_visible(False)

# ---------- G: modal frequencies ----------
ax = fig.add_subplot(gs[1, 3])
ax.axvspan(0.2, 1.0, color="#e8d9c4", alpha=0.7, zorder=0)
ax.axvspan(1.0, 5.0, color="#dcebd2", alpha=0.5, zorder=0)
ylab = []
off = {0: 0.27, 1: 0.09, 2: -0.09, 3: -0.27}
names = {0: "ствол", 1: "сук", 2: "ветвь", 3: "веточка"}
for yi, d in enumerate(("HEAD", "r12", "P18", "SEG24")):
    for mm in R["E1"][d]["modes"][:14]:
        ax.scatter(mm["f"], yi + off[mm["order"]], s=26 + 70 * min(mm["zeta"], 0.5), color=C_ORD[mm["order"]],
                   edgecolors="black", linewidths=0.3, zorder=3)
ax.set_yticks(range(4))
ax.set_yticklabels(["HEAD\n(сейчас)", "r12\n(req-269)", "P18\n(req-287)", "SEG24\n(предл.)"], fontsize=8)
ax.set_xscale("log"); ax.set_xlim(0.15, 30); ax.set_ylim(-0.6, 4.35)
ax.set_xlabel("собственная частота, Гц")
for o in range(4):
    ax.scatter([], [], color=C_ORD[o], s=30, edgecolors="black", linewidths=0.3, label=names[o])
ax.legend(loc="upper center", ncol=4, frameon=True, framealpha=0.9, title="звено (размер точки ~ затухание)", handletextpad=0.2, columnspacing=0.8,
          title_fontsize=7.2)
ax.set_title("Собственные частоты по звеньям\n(полосы: ствол 0,2–1 Гц, ветви быстрее)", fontsize=10)
ax.grid(axis="x", alpha=0.25, which="both")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
fig.savefig(OUT, dpi=105)
print("saved", OUT)
