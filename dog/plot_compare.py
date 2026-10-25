"""plot_compare.py - Unity take vs MuJoCo kinematic replay (Russian labels) -> compare.png"""
import csv, numpy as np, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

def load(p):
    r = list(csv.DictReader(open(p)))
    return {k: np.array([float(x[k]) for x in r]) for k in r[0]}

A = load("replay_x_kp3.csv"); Bc = load("replay_x_capped.csv"); T = load("replay_t1_kp3.csv")
fig, ax = plt.subplots(4, 1, figsize=(11, 11), sharex=True)
s = A["t"] >= 1.0
ax[0].plot(A["t"][s], (A["u_chestY"][s] - A["u_chestY"][0]) * 100, "k", lw=1, label="Unity")
ax[0].plot(A["t"][s], (A["m_chestY"][s] - A["u_chestY"][0]) * 100, "r", lw=1, label="MuJoCo-двойник")
ax[0].set_ylabel("высота груди, см"); ax[0].legend(loc="upper right")
ax[0].set_title("Собака: Unity (дубль cl505_x_s07, рысь) против MuJoCo (повтор записанных углов суставов)")
ax[1].plot(A["t"][s], A["u_pitch"][s], "k", lw=1, label="Unity: наклон груди")
ax[1].plot(A["t"][s], A["m_pitch"][s], "r", lw=1, label="MuJoCo: наклон груди")
ax[1].plot(A["t"][s], A["u_pelvis"][s], "k--", lw=0.8, label="Unity: таз")
ax[1].plot(A["t"][s], A["m_pelvis"][s], "r--", lw=0.8, label="MuJoCo: таз")
ax[1].set_ylabel("угол, град"); ax[1].legend(loc="lower right", fontsize=8, ncol=2)
ax[2].plot(A["t"][s], A["u_comX"][s] - A["u_comX"][s][0], "k", label="Unity")
ax[2].plot(A["t"][s], A["m_comX"][s] - A["m_comX"][s][0], "r", label="MuJoCo, точное слежение (+18 %)")
ax[2].plot(Bc["t"][Bc["t"] >= 1], Bc["m_comX"][Bc["t"] >= 1] - Bc["m_comX"][Bc["t"] >= 1][0], "b", label="MuJoCo, мышцы с потолками игры")
ax[2].set_ylabel("путь центра масс, м"); ax[2].legend(loc="upper left")
ax[3].plot(A["t"][s], A["u_paws"][s], "k", lw=1, label="Unity: лап на земле")
ax[3].plot(A["t"][s], A["m_paws"][s] + 0.05, "r", lw=1, label="MuJoCo (+0.05 для видимости)")
ax[3].set_ylabel("лап на земле"); ax[3].set_xlabel("время, с"); ax[3].legend(loc="lower right", fontsize=8)
for a in ax: a.grid(alpha=0.3)
txt = ("окно 1.5-12 с, дубль x: высота груди ск.откл. 0.35 см, наклон 0.96°, таз 0.90°, совпадение лап 81 %\n"
       "дубль t1: 0.80 см, 2.4°, 2.5°, 69 %.  Скорость: Unity 0.70 м/с, MuJoCo 0.82 м/с - лапы в Unity скользят в 2.2-2.7 раза больше")
fig.text(0.5, 0.005, txt, ha="center", fontsize=9)
plt.tight_layout(rect=(0, 0.04, 1, 1)); plt.savefig("compare.png", dpi=90)
