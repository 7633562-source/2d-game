"""eval_zero.py - the game's trot joint profiles alone (zero residual) through the twin, eval_dog metrics."""
import json, sys, numpy as np
sys.path.insert(0, '.')
import rlenv_dog, eval_dog
for dr in (0, 1):
    env = rlenv_dog.DogTrot(rv=2, max_t=eval_dog.T1, dr=bool(dr), seed=7000)
    env.fixed_plan = [(0.0, 1.0), (eval_dog.V, 1e9)]
    res = []
    for ep in range(10):
        env.reset(seed=7000 + ep); info = {}
        while True:
            o, r, te, tr, info = env.step(np.zeros(13))
            if te or tr: break
        res.append(eval_dog.metrics(env, info))
    s = dict(dr=dr, success=sum(r["success"] for r in res), falls=sum(r["fell"] for r in res),
             speed=round(float(np.mean([r["speed"] for r in res])), 3), diag=round(float(np.mean([r["diag_agree"] for r in res])), 3),
             stride=res[0].get("stride_s"), duty=res[0].get("duty"), slip=res[0].get("slip_m_per_paw_s"),
             assist=round(float(np.mean([r["assist_bw_pct"] for r in res])), 2))
    print(json.dumps(s))
    open("eval_zero.jsonl", "a").write(json.dumps(dict(summary=s, episodes=res)) + "\n")
