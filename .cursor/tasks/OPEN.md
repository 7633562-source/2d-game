# Open cards

Door for what is actually open: `CURRENT.md`. This table is a file index;
several listed cards are accepted leftovers. Do not start a listed card
without reading `CURRENT.md` and the card body.

| File | Agent |
|---|---|
| `VEST-2-balance.md` | physics — per-foot quiet ankle; VEST-1 archived |
| `WALK-1-physics.md` | physics (paused: vestibular first) |
| `WALK-2.md` | physics — CaptureWalk canonical (`-captureWalk 1`); FallCatch card leftover |
| `VIS-HAND.md` | visual (stance accepted; Play after build) |
| `PERF-1-optimization.md` | optimization — lab + `opt_agent_proposals.md` (pre-grove, pre-yard) |
| `OPT-3-distant-rest.md` | optimization — off-camera NPC rest (`NpcRest`); hero stays 200 Hz |
| `PLANT-1-tree.md` | plant (sway tree in scene `Tree`) |
| `PLANT-3-flowers.md` | plant (12 garden kinds, picture only) |
| `VIS-TREE.md` | visual (bark/leaf on; Play scene `Tree`) |
| `VIS-FOOT.md` | visual — foot art stretched ×2.42, collider stays 0.26×0.07 |
| `VIS-DOG-textures.md` | visual — painted dog parts, sizes unchanged |
| `VIS-BIRD-textures.md` | visual — crow and chicken parts, flight unchanged |
| `DOCS-DAILY.md` | docs (nightly 02:00) |
| `DOG-1-physics.md` | dog — first 52 cycles; leftover c39 sit, c52 columns fold |
| `DOG-2-stance-10.md` | dog — c53–c62 done; leftover rake −25/+25 + startupHip 8/0.25, drop 0.193 sit |
| `DOG-3-walk.md` | dog — clocked walk ≥ 5 km/h; four limb poses; stable |
| `DOG-4-ai.md` | ai + dog — `DogDrive` writes Walk/Leap; no keyboard |
| `BIRD-2-sit-attack.md` | bird + ai — Sit / Attack; prey `BirdDrive` |
| `BIRD-3-wander.md` | ai — random Sit / Fly / turn / land |
| `BIRD-4-flock.md` | ai — `BirdFlockDrive` |
| `BIRD-5-perch-return.md` | ai — sit on the tree slot again |
| `BIRD-6-voice.md` | ai — vocalize; threat → Alarm then Fly |
| `BIRD-7-chicken.md` | bird + ai — `BirdKind.Chicken`; Play `Chicken` |
| `BIRD-8-beak.md` | bird + ai — beak segment; hen Peck; BeakStrike |
| `BIRD-9-tree-ghost.md` | bird + plant — fly through wood; sit on `BirdPerch` |
| `BIRD-10-life.md` | ai — Forage default; stronger → Flee; weaker → Pursue |
| `BIRD-11-walk.md` | ai — crow Walk on dirt while foraging |
| `BIRD-12-core-contact.md` | bird — inter-bird contact torso/head only; walk = body thrust |
| `BIRD-13-insect-hunt.md` | ai — Forage + see Insects → Pursue / Peck |
| `FACTION-1.md` | ai — stamp Player / Wildlife.Wolf / Wildlife.Bird at spawn |

`ai` — bird brain; wander, flock, voice, hen, beak, perch, life, crow walk, insect hunt (`BIRD-3`…`BIRD-13`).
`bird` — flap / landing / BIRD-12 core contact.
Factions: `FactionMember` at spawn (`FACTION-1`). Wolf vs Bird is Hostile.
No Vision scan without a card.

`docs` — standing role: `DOCS.md`, pointers to `stable-pd/`, do not delete
summaries. Night 02:00: `Unity-Docs-daily` → `Tools/run-docs-daily.ps1`.

Every profile also reads `Trials/Runs/opt_agent_proposals.md` (own
section). Not a GO card.

# Deferred — do NOT start

`docs/ENGINE-6.6.md` — engine channel 6.6 / Unity 2D digest (H_Scout 17.09).
Held until the Grok burn campaign ends on 29.09: the editor, URP and the
Physics 2D under the trial baselines must not move mid-flight. Read the file
before proposing anything from it. Owner: Hermes.

Everything else is `archive/`.
Documents are English only, always.
