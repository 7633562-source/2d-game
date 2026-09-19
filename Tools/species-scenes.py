#!/usr/bin/env python3
"""One-species scenes for the plant lane (Yury 18.09: a scene per species, no mixing).

The apple scene (Assets/Scenes/TreeApple.unity) is the proven template: a copy of
Tree.unity whose GameProcess spawn config is narrowed to ONE species, plus the
SpeciesScene component that owns the camera and keeps the WHOLE tree in frame.
This tool clones that scene for the remaining species and only rewrites the three
fields that name the species, so nothing else can drift:

    GameProcess.treeSpecies   apple -> oak|pine
    SpeciesScene.speciesName  apple -> oak|pine
    SpeciesScene.label        treeapple -> treeoak|treepine

It also writes a .meta with a fixed guid (so the build-settings entry and the
imported asset agree from the first import) and registers the scene in
ProjectSettings/EditorBuildSettings.asset.

  python Tools/species-scenes.py            # write the scenes
  python Tools/species-scenes.py --check    # verify, write nothing

No Unity API, no physics: scene data only. The tree rigs and GameProcess are
read-only inputs here.
"""
from __future__ import annotations

import argparse
import hashlib
import re
import sys
import uuid
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
SCENES = PROJECT / "Assets" / "Scenes"
TEMPLATE = SCENES / "TreeApple.unity"
BUILD_SETTINGS = PROJECT / "ProjectSettings" / "EditorBuildSettings.asset"

# species -> (scene file, speciesName, label)
SPECIES = {
    "oak": ("TreeOak.unity", "oak", "treeoak"),
    "pine": ("TreePine.unity", "pine", "treepine"),
}

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def stable_guid(name: str) -> str:
    """Deterministic 32-hex guid, so re-running the tool never re-imports."""
    return uuid.uuid5(uuid.NAMESPACE_URL, "hermes-2dgame/" + name).hex


def render(template: str, species: str, label: str) -> str:
    out = template
    for old, new, where in (
        ("  treeSpecies: apple", "  treeSpecies: " + species, "GameProcess.treeSpecies"),
        ("  speciesName: apple", "  speciesName: " + species, "SpeciesScene.speciesName"),
        ("  label: treeapple", "  label: " + label, "SpeciesScene.label"),
    ):
        if out.count(old) != 1:
            raise SystemExit("template line %r appears %d times (want 1)" % (where, out.count(old)))
        out = out.replace(old, new)
    return out


def register(scene_guid: dict[str, str], dry: bool) -> list[str]:
    text = BUILD_SETTINGS.read_text(encoding="utf-8")
    added: list[str] = []
    for path, guid in scene_guid.items():
        if path in text:
            continue
        text = text.replace(
            "  m_configObjects:",
            "  - enabled: 1\n    path: {0}\n    guid: {1}\n  m_configObjects:".format(path, guid),
            1,
        )
        added.append(path)
    if added and not dry:
        BUILD_SETTINGS.write_text(text, encoding="utf-8")
    return added


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="verify only, write nothing")
    args = ap.parse_args()

    if not TEMPLATE.exists():
        print("FAIL no template at", TEMPLATE)
        return 1
    template = TEMPLATE.read_text(encoding="utf-8")
    seen_guids: dict[str, str] = {}
    problems: list[str] = []

    for species, (filename, species_name, label) in sorted(SPECIES.items()):
        path = SCENES / filename
        guid = stable_guid(filename)
        body = render(template, species_name, label)
        meta = META_TEMPLATE.format(guid=guid)

        if args.check:
            if not path.exists():
                problems.append("missing " + filename)
                continue
            have = path.read_text(encoding="utf-8")
            if have != body:
                problems.append(filename + " differs from what the template would write")
        else:
            path.write_text(body, encoding="utf-8")
            (SCENES / (filename + ".meta")).write_text(meta, encoding="utf-8")

        # The three fields must be the ONLY species-related difference and must
        # be exactly one line each - no mixed scene can come out of this.
        for marker in ("treeSpecies: " + species_name, "speciesName: " + species_name,
                       "label: " + label):
            if body.count(marker) != 1:
                problems.append(filename + ": expected exactly one " + repr(marker))
        if body.count("treeCount: 1") != 1:
            problems.append(filename + ": treeCount must be 1")
        for no_other_species in ("humanCount: 0", "birdCount: 0", "dogCount: 0",
                                 "chickenCount: 0", "flowerCount: 0", "spawnCampfire: 0"):
            if no_other_species not in body:
                problems.append(filename + ": missing " + no_other_species)

        seen_guids["Assets/Scenes/" + filename] = guid
        print("SPECIES {0} -> {1} guid {2} bytes {3}".format(species, filename, guid, len(body)))

    added = register(seen_guids, args.check)
    print("BUILD-SETTINGS added:", added if added else "none (already registered)")
    settings = BUILD_SETTINGS.read_text(encoding="utf-8")
    for path, guid in sorted(seen_guids.items()):
        if path not in settings or guid not in settings:
            problems.append("build settings missing " + path)

    if problems:
        for p in problems:
            print("FAIL", p)
        return 1
    print("SPECIES-SCENES OK files=%d mode=%s" % (len(SPECIES), "check" if args.check else "write"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
