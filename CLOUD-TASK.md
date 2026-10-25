# Cloud task trees-1025 (Fable): complex, realistic trees with moving foliage that do not kill performance

Owner (verbatim, 25.10): «давай сделаем деревья более сложными и реалистичныи, больше сгментов, добавим шевелящуюся листву, но чтобы такие деревья не убили производительнгость игры»

Snapshot = main 3f906b53 code + patches/req-459-2 applied (the tall tree K4L5g, rep-459 PASS: 4.1 m, bole 6, depth 4, E44, floor 1.3, sag 4, -treeMarginCeil 1.0, leaves 3.05/0.05). Unity 6000.6.4f1, 2D, Box2D (Rigidbody2D + HingeJoint2D), URP 2D. Lab notes: lab/tree-1019 (numpy articulated-tree model, audit, two request drafts), lab/reports.

Deliver (as ONE private Artifact titled "Trees cloud 1025", code as full files or unified diffs against this snapshot):
1. Design: physics skeleton with more segments (tapered 2-piece limbs per lab/tree-1019 req-draft-tree-branchseg.md idea, cap ~60 bodies per tree), stable at the game's fixed step (stability margin k <= m I / dt^2 per joint), sway bands as rep-459.
2. Foliage WITHOUT physics bodies: many leaves/leaf cards rendered in batches (one material, GPU instancing or a single mesh per tree), animated in a vertex shader (URP 2D compatible) from the parent branch's motion + the TreeWindField wind + per-leaf phase noise. Replaces the leaf-cluster bodies when the new flag is on.
3. Code behind default-off flags (e.g. -treeSkel2, -treeFoliage2); with none of them the build must be byte-identical. Name every new flag and its default.
4. Performance budget: <= 1 ms CPU per tree per frame and a plan to measure it headless (bodies, joints, draw calls, ms) for 1 / 5 / 10 trees.
5. A short risk list (Box2D joint chains, leaf popping, crows perching on new segments - BirdTriggerPerch / PlantTree.ApplyPerchLoad).
No push needed: the result is the Artifact.
