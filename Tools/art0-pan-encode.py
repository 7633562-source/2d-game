"""ART-0 pan encoder: the capture writes shot_<label>_<n>.png (unpadded, n = 0..N),
ffmpeg's image2 demuxer needs a fixed width, so this copies the run into a padded
sequence in a scratch folder and encodes the mp4 next to the stills.

Usage:  python Tools/art0-pan-encode.py <label> [fps]
"""
import os
import shutil
import subprocess
import sys

PROJ = r"E:/Games/2D Game/My project"
RUNS = os.path.join(PROJ, "Trials/Runs")


def main(label, fps=10):
    frames = sorted((int(fn.rsplit("_", 1)[1][:-4]), fn) for fn in os.listdir(RUNS)
                    if fn.startswith("shot_%s_" % label) and fn.endswith(".png"))
    if not frames:
        print("no frames for %s" % label)
        return 1
    nums = [n for n, _ in frames]
    print("frames=%d index %d..%d" % (len(frames), min(nums), max(nums)))

    pad = os.path.join(RUNS, "_pan_%s" % label)
    if os.path.isdir(pad):
        shutil.rmtree(pad)
    os.makedirs(pad)
    for i, (n, fn) in enumerate(frames):
        shutil.copyfile(os.path.join(RUNS, fn), os.path.join(pad, "pan_%03d.png" % i))

    out = os.path.join(RUNS, "%s.mp4" % label)
    pattern = os.path.join(pad, "pan_%03d.png").replace("\\", "/")
    cmd = ["ffmpeg", "-y", "-framerate", str(fps), "-i", pattern,
           "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", out]
    r = subprocess.run(cmd, capture_output=True, text=True)
    print("ffmpeg exit=%d" % r.returncode)
    if r.returncode:
        print(r.stderr[-600:])
        return r.returncode

    probe = subprocess.run(["ffprobe", "-v", "error", "-show_entries",
                            "format=duration,size:stream=nb_frames,width,height,codec_name",
                            "-of", "default=noprint_wrappers=1", out],
                           capture_output=True, text=True)
    print(probe.stdout.strip())
    print("PAN-ENCODE OK %s (%d bytes)" % (out, os.path.getsize(out)))
    shutil.rmtree(pad)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 10))
