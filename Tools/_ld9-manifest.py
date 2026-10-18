import json
p = r"E:/Games/2D Game/My project/Build/Headless/build-manifest.json"
d = json.load(open(p))
for k in ("fingerprint", "buildGuid", "buildTimeUtc", "gitCommit", "gitDirty", "outputPath"):
    if k in d:
        print("%s: %s" % (k, d[k]))
