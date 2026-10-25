import sys, json
sys.path.insert(0, r"D:\hermes-data\claude-lab\cache\dog-twin-1024")
import twin_dog as T
take = sys.argv[1] if len(sys.argv) > 1 else "cl505_x_s07.csv"
tag = sys.argv[2] if len(sys.argv) > 2 else "x"
cases = {
 "base": {},
 "kp3": dict(kp_scale=3.0),
 "ell": dict(bkw=dict(cone="elliptic", impratio=10)),
 "claw": dict(paw_mu=4.0),
 "skin0": dict(skin=0.0),
 "mu02": dict(paw_mu=0.1),
 "capped": dict(capped=True),
 "capped_assist": dict(capped=True, assist=dict(tilt0=-1.8, dead=4.0, cap=4.9)),
 "kp3_ell": dict(kp_scale=3.0, bkw=dict(cone="elliptic", impratio=10)),
}
rows = []
for k, kw in cases.items():
    r = T.replay(T.RUNS + "/" + take, tag + "_" + k, t_max=12.0, **kw)
    w = r["walk"]
    rows.append((k, w["chestY_rms_cm"], w["chestY_bias_cm"], w["pitch_rms_deg"], w["pelvis_rms_deg"], w["u_speed"], w["m_speed"], w["paw_flag_agree"], w["m_body_touch_frac"], w["track_err_deg_rms"]))
    rows[-1] = rows[-1] + (w["m_slip_per_paw_mps"],)
    print("%-14s chY %.2f (bias %+.2f) cm  pitch %.2f  pelvis %.2f deg  v U %.3f M %.3f  flags %.2f  touch %.2f  trk %.1f slip %.3f" % rows[-1], flush=True)
json.dump(rows, open(r"D:\hermes-data\claude-lab\cache\dog-twin-1024\sweep_%s.json" % tag, "w"), indent=1)
