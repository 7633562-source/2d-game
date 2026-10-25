"""plot_train.py runs/<tag> [...] -> progress_dog.png (Russian labels)"""
import sys, csv, numpy as np, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
fig, ax = plt.subplots(2, 2, figsize=(12, 7))
for p in sys.argv[1:]:
    r = list(csv.DictReader(open(p + "/progress.csv")))
    s = np.array([float(x["steps"]) for x in r]) / 1e6
    g = lambda k: np.array([float(x[k]) for x in r])
    lab = p.split("/")[-1]
    ax[0, 0].plot(s, g("ep_rew_mean"), label=lab)
    ax[0, 1].plot(s, g("speed_mean"), label=lab)
    ax[1, 0].plot(s, g("fell_frac"), label=lab)
    ax[1, 1].plot(s, 100 * g("assist_bw_mean"), label=lab)
ax[0, 0].set_title("награда за эпизод (12 с)")
ax[0, 1].set_title("средняя скорость за эпизод, м/с (цель 0.8 после стойки и разгона)"); ax[0, 1].axhline(0.8 * 0.85, ls="--", c="gray")
ax[1, 0].set_title("доля эпизодов с падением")
ax[1, 1].set_title("помощь (рука на тазу), % веса тела")
for a in ax.flat: a.set_xlabel("шаги обучения, млн"); a.grid(alpha=0.3); a.legend(fontsize=8)
fig.suptitle("Собака-двойник MuJoCo: обучение рыси PPO (4 процесса, рандомизация физики)")
plt.tight_layout(); plt.savefig("progress_dog.png", dpi=90)
