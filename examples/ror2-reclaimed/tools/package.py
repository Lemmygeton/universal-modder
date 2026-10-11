#!/usr/bin/env python3
"""Build Reclaimed in Release and pack the Thunderstore zip.

    python3 tools/package.py              # dotnet build -c Release, then build/Reclaimed-<version>.zip
    python3 tools/package.py --no-build   # pack the DLLs already in src/*/bin/Release

Layout of the zip (what r2modman expects):

    manifest.json  icon.png  README.md  CHANGELOG.md
    plugins/Reclaimed/Reclaimed.Core.dll, Reclaimed.<Survivor>.dll

Only our own assemblies are packed. Game, BepInEx, R2API and MMHOOK DLLs are references, never shipped;
the script refuses to pack if any of them shows up in the plugin folder.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STORE = ROOT / "thunderstore"
SRC = ROOT / "src"
OUT = ROOT / "build"

REQUIRED = ["manifest.json", "icon.png", "README.md", "CHANGELOG.md"]
FORBIDDEN_PREFIXES = ("RoR2", "Assembly-CSharp", "R2API", "MMHOOK", "BepInEx", "0Harmony", "Mono.", "MonoMod",
                      "Unity", "HG", "KinematicCharacterController", "Rewired", "LegacyResourcesAPI", "AK.", "Ak.")


def fail(msg: str) -> None:
    print(f"package: error: {msg}", file=sys.stderr)
    sys.exit(1)


def check_store() -> dict:
    missing = [n for n in REQUIRED if not (STORE / n).is_file()]
    if missing:
        hint = " (generate it with tools/make_icon.py or add a 256x256 PNG)" if "icon.png" in missing else ""
        fail(f"missing in thunderstore/: {', '.join(missing)}{hint}")
    manifest = json.loads((STORE / "manifest.json").read_text(encoding="utf-8"))
    for key in ("name", "version_number", "description", "dependencies"):
        if key not in manifest:
            fail(f"manifest.json has no '{key}'")
    if len(manifest["description"]) > 250:
        fail("manifest.json description is longer than Thunderstore's 250 characters")
    icon = (STORE / "icon.png").read_bytes()
    if icon[:8] != b"\x89PNG\r\n\x1a\n":
        fail("icon.png is not a PNG")
    width, height = int.from_bytes(icon[16:20], "big"), int.from_bytes(icon[20:24], "big")
    if (width, height) != (256, 256):
        fail(f"icon.png must be 256x256, got {width}x{height}")
    return manifest


def build() -> None:
    cmd = ["dotnet", "build", str(ROOT / "Reclaimed.sln"), "-c", "Release", "-nologo", "-v", "q"]
    print("package:", " ".join(cmd))
    if subprocess.run(cmd, cwd=ROOT).returncode != 0:
        fail("dotnet build failed")


def plugin_dlls() -> list[Path]:
    dlls = []
    for proj in sorted(SRC.glob("Reclaimed.*/*.csproj")):
        name = proj.stem
        dll = proj.parent / "bin" / "Release" / f"{name}.dll"
        if not dll.is_file():
            fail(f"{dll.relative_to(ROOT)} not found; build first or drop --no-build")
        dlls.append(dll)
    for dll in dlls:
        if dll.name.startswith(FORBIDDEN_PREFIXES):
            fail(f"refusing to ship {dll.name}: game/loader/API assemblies are never packaged")
    return dlls


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--no-build", action="store_true", help="pack the existing Release build")
    args = ap.parse_args()

    manifest = check_store()
    if not args.no_build:
        build()
    dlls = plugin_dlls()

    OUT.mkdir(exist_ok=True)
    zip_path = OUT / f"{manifest['name']}-{manifest['version_number']}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for name in REQUIRED:
            z.write(STORE / name, name)
        for dll in dlls:
            z.write(dll, f"plugins/{manifest['name']}/{dll.name}")
    print(f"package: wrote {zip_path.relative_to(ROOT)}")
    for dll in dlls:
        print(f"  plugins/{manifest['name']}/{dll.name}  ({dll.stat().st_size // 1024} KiB)")


if __name__ == "__main__":
    main()
