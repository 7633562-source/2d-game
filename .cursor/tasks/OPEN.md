# Open cards

| File | Agent |
|---|---|
| `WALK-1-physics.md` | physics |
| `VIS-HAND.md` | visual (stance accepted; Play after build) |
| `PERF-1-optimization.md` | optimization — lab + `opt_agent_proposals.md` (pre-grove) |
| `PLANT-1-tree.md` | plant (sway tree in scene `Tree`) |
| `VIS-TREE.md` | visual (bark/leaf on; Play scene `Tree`) |
| `VIS-FOOT.md` | visual — foot art stretched ×2.42, collider stays 0.26×0.07 |
| `VIS-DOG-textures.md` | visual — painted dog parts, sizes unchanged |
| `VIS-BIRD-textures.md` | visual — crow and chicken parts, flight unchanged |
| `DOCS-DAILY.md` | docs (nightly 02:00) |
| `DOG-1-physics.md` | dog — first 52 cycles; leftover c39 sit, c52 columns fold |
| `DOG-2-stance-10.md` | dog — c53–c62 done; leftover rake −25/+25 + startupHip 8/0.25, drop 0.193 sit |
| `BIRD-2-sit-attack.md` | bird + ai — Sit / Attack; prey `BirdDrive` |
| `BIRD-3-wander.md` | ai — random Sit / Fly / turn / land |
| `BIRD-4-flock.md` | ai — `BirdFlockDrive` |
| `BIRD-5-perch-return.md` | ai — sit on the tree slot again |
| `BIRD-6-voice.md` | ai — vocalize; threat → Alarm then Fly |
| `BIRD-7-chicken.md` | bird + ai — `BirdKind.Chicken`; Play `Chicken` |
| `BIRD-8-beak.md` | bird + ai — beak segment; hen Peck; BeakStrike |
| `BIRD-9-tree-ghost.md` | bird + plant — fly through wood; sit on `BirdPerch` |
| `BIRD-10-life.md` | ai — Forage default; stronger → Flee; weaker → Pursue |
| `FACTION-1.md` | ai — stamp Player / Wildlife.Wolf / Wildlife.Bird at spawn |

`ai` — bird brain; wander, flock, voice, hen, beak, perch, life (`BIRD-3`…`BIRD-10`).
Factions: `FactionMember` at spawn (`FACTION-1`). Wolf vs Bird is Hostile.
No Vision scan without a card.

`docs` — standing role: `DOCS.md`, pointers to `stable-pd/`, do not delete
summaries. Night 02:00: `Unity-Docs-daily` → `Tools/run-docs-daily.ps1`.

Every profile also reads `Trials/Runs/opt_agent_proposals.md` (own
section). Not a GO card.

Everything else is `archive/`.
Documents are English only, always.
