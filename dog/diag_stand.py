import sys, math, mujoco, numpy as np
sys.path.insert(0, r"D:\hermes-data\claude-lab\cache\dog-twin-1024")
import twin_dog as T, build_dog as B
for kw in [dict(noslip=10), dict(cone="elliptic", impratio=10), dict(solref="0.005 1"), dict(cone="elliptic", impratio=10, noslip=5)]:
    for sc in (0.15,):
        m,d,sy=T.load_model(**kw); ids=T.Ids(m); kp,kv=T.gains(sc); T.to_servo(m,kp,kv,cap=T.ceilings())
        act={jn:mujoco.mj_name2id(m,mujoco.mjtObj.mjOBJ_ACTUATOR,"m_"+jn) for jn in B.JOINTS_ORDER}
        xs=[]
        for k in range(1000):
            tgt={jn:0.0 for jn in B.JOINTS_ORDER}
            for p in T.PAWS: tgt[p]=T.paw_flat_target(d,ids,p)
            tgt.update(T.set_neck_targets(d,ids))
            for jn,v in tgt.items():
                if act[jn]>=0: d.ctrl[act[jn]]=v
            mujoco.mj_step(m,d)
            if k%200==199: xs.append((round(d.subtree_com[1][0],4), [round(d.xpos[ids.paw_b[p]][0],3) for p in T.PAWS], round(math.degrees(d.qpos[2]),2)))
        print(kw, sc, xs[0], xs[-1])
