# VIS-TREE — bark and leaf

One sentence: tree segments should read as bark and foliage, not as a
tinted human capsule.

Layer: picture (`HumanSegment` Visual, `ArtLibrary`). Do not touch physics.

Contract:
- Do not change `PlantTree` sizes (`trunkWidth`, `leafSize`, `collider.size`).
- No `localScale`. Bake albedo like clothing, or add an `ArtLibrary` key.
- A leaf is `InitializeVisualOnly`, no `Rigidbody2D`.
- Do not open `SampleScene` / `World` / human stance.

Acceptance: scene `Tree`, Play. Colliders and body count stay the same.
Do not run a human CSV.

Done 30.08: `ArtLibrary.Bark` / `Leaf`, wood and visual-only leaves bake
albedo like clothing. Sizes not changed.

Forbidden: one full-tree texture painted over the segments.
