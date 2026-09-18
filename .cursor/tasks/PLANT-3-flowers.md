# PLANT-3 — garden flowers, picture only

One sentence: scene `Tree` also has a bed of small plants; each kind is
its own subclass of `PlantFlower`.

Layer: plants (`PlantFlower`, `FlowerRose`…`FlowerBluebell`).
Not `PlantTree`. Not the yard.

Contract:
- Do not touch `SampleScene`. `flowerCount` stays 0 there and in `World`.
- No `Rigidbody2D`, collider, spring, wind, or `BirdPerch`.
- Size is `sprite.size`, not `localScale`.
- Sit contact stays on tree pads. A bird does not sit on a bloom.
- Do not retune human PD or 200 Hz.

Kinds (12): Rose, Tulip, Daisy, Poppy, Sunflower, Lily, Daffodil,
Violet, Crocus, Lavender, Iris, Bluebell. `KindFromIndex` cycles them.
Play `Tree` plants all twelve (`flowerCount = 12`). Headless:
`-flower 1` or `-flower 1 -flowerKind rose`.

Acceptance (04.09, fingerprint `db775813…3288b5b`):
- Twelve subclasses exist; each locks `FlowerKind` in `Awake`.
- `-flower 1` → `flower_kinds.json`: count 12, kinds Rose…Bluebell,
  `flowerPartCount` 62, `maxStemHeight` 0.42 (sunflower).
- `-flower 1 -flowerKind rose` → `flower_rose.json`: count 1, stem 0.22.
- No wood joints, no perch slots.

Textures are not this card (`VIS-TREE` is bark/leaf). A painted petal
set is a later artist card. A World flower bed is a later level card.
