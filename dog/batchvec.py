"""batchvec.py - SubprocVecEnv variant with K envs per worker process.

Windows pipe round-trips cost ~0.6 ms per vec step, more than one env step (~0.2 ms), so one env per process
caps 8 workers at ~10 k steps/s. With K envs per process the round-trip is shared by K envs.
Auto-reset semantics match SB3 (info["terminal_observation"], info["TimeLimit.truncated"])."""
import multiprocessing as mp

import numpy as np
from stable_baselines3.common.vec_env.base_vec_env import CloudpickleWrapper
from stable_baselines3.common.vec_env.base_vec_env import VecEnv


def _worker(remote, parent_remote, fns):
    parent_remote.close()
    envs = [f() for f in fns.var]
    try:
        while True:
            cmd, data = remote.recv()
            if cmd == "step":
                out = []
                for e, a in zip(envs, data):
                    o, r, te, tr, info = e.step(a)
                    done = te or tr
                    if done:
                        info["terminal_observation"] = o
                        info["TimeLimit.truncated"] = tr and not te
                        o, _ = e.reset()
                    out.append((o, r, done, info))
                remote.send(out)
            elif cmd == "reset":
                remote.send([e.reset()[0] for e in envs])
            elif cmd == "get_attr":
                remote.send([getattr(e, data) for e in envs])
            elif cmd == "set_attr":
                for e in envs:
                    setattr(e, data[0], data[1])
                remote.send(None)
            elif cmd == "method":
                name, args, kw = data
                remote.send([getattr(e, name)(*args, **kw) for e in envs])
            elif cmd == "close":
                remote.close()
                break
    except KeyboardInterrupt:
        pass


class BatchSubprocVecEnv(VecEnv):
    def __init__(self, env_fns, n_workers):
        self.n = len(env_fns)
        assert self.n % n_workers == 0
        k = self.n // n_workers
        self.k = k
        ctx = mp.get_context("spawn")
        self.remotes, wr = zip(*[ctx.Pipe() for _ in range(n_workers)])
        self.procs = []
        for i, (r, w) in enumerate(zip(self.remotes, wr)):
            p = ctx.Process(target=_worker, args=(w, r, CloudpickleWrapper(env_fns[i * k:(i + 1) * k])), daemon=True)
            p.start()
            self.procs.append(p)
            w.close()
        e0 = env_fns[0]()
        super().__init__(self.n, e0.observation_space, e0.action_space)
        e0.close()

    def step_async(self, actions):
        for i, r in enumerate(self.remotes):
            r.send(("step", actions[i * self.k:(i + 1) * self.k]))

    def step_wait(self):
        res = [x for r in self.remotes for x in r.recv()]
        o, rw, d, inf = zip(*res)
        return np.stack(o), np.array(rw, dtype=np.float32), np.array(d), list(inf)

    def reset(self):
        for r in self.remotes:
            r.send(("reset", None))
        return np.stack([x for r in self.remotes for x in r.recv()])

    def close(self):
        for r in self.remotes:
            try:
                r.send(("close", None))
            except Exception:
                pass
        for p in self.procs:
            p.join(timeout=5)

    def get_attr(self, name, indices=None):
        for r in self.remotes:
            r.send(("get_attr", name))
        vals = [x for r in self.remotes for x in r.recv()]
        return vals if indices is None else [vals[i] for i in self._get_indices(indices)]

    def set_attr(self, name, value, indices=None):
        for r in self.remotes:
            r.send(("set_attr", (name, value)))
        for r in self.remotes:
            r.recv()

    def env_method(self, name, *args, indices=None, **kw):
        for r in self.remotes:
            r.send(("method", (name, args, kw)))
        vals = [x for r in self.remotes for x in r.recv()]
        return vals if indices is None else [vals[i] for i in self._get_indices(indices)]

    def env_is_wrapped(self, wrapper_class, indices=None):
        return [False] * (self.n if indices is None else len(list(self._get_indices(indices))))

    def get_images(self):
        return []
