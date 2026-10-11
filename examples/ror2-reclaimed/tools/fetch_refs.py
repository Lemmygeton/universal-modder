#!/usr/bin/env python3
"""Fetch the reference assemblies Reclaimed compiles against into ./refs (gitignored).

Two sources:
  default        nuget.org only: R2API modules, MMHOOK.RoR2, RoR2BepInExPack, UnityEngine.Modules,
                 BepInEx 5.4.22 core and RiskOfRain2.GameLibs. nuget.org only carries GameLibs 1.3.1,
                 which predates DamageSource, so `tools/RefTools shim` adds that API. Good for CI and
                 compile checks; `tools/RefTools apicheck` then checks the result against the current
                 game's signatures (MMHOOK_RoR2 is regenerated for every game patch).
  --game DIR     the user's own install (DIR = folder holding "Risk of Rain 2_Data") plus
  --bepinex DIR  a BepInEx folder (r2modman profile or game folder). This is the exact current API and
                 is what release builds should use when nuget.bepinex.dev is not reachable.

Usage:
  python3 tools/fetch_refs.py                       # nuget.org refs + shim
  python3 tools/fetch_refs.py --game "C:/.../Risk of Rain 2" --bepinex "C:/.../profiles/Default/BepInEx"
Then: dotnet build -c Release -p:RefsMode=Local
"""
import argparse
import io
import json
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REFS = ROOT / "refs"
CACHE = REFS / ".cache"
FLAT = "https://api.nuget.org/v3-flatcontainer"

# id, version, sub-folder inside refs/
PACKAGES = [
    ("riskofrain2.gamelibs", "1.3.1.275-r.0", "game"),
    ("unityengine.modules", "2021.3.33", "unity"),
    ("mmhook.ror2", "2026.10.8", "mmhook"),
    ("ror2bepinexpack", "1.45.0", "bepinex"),
    ("talespire.bepinex", "5.4.22", "bepinex"),  # generic BepInEx 5.4.22 core + HarmonyX + MonoMod
    ("r2api.core", "5.3.0", "r2api"),
    ("r2api.prefab", "1.1.1", "r2api"),
    ("r2api.language", "1.1.0", "r2api"),
    ("r2api.unlockable", "1.0.2", "r2api"),
    ("r2api.sound", "1.0.3", "r2api"),
    ("r2api.damagetype", "1.1.7", "r2api"),
    ("r2api.recalculatestats", "1.6.6", "r2api"),
    ("r2api.contentmanagement", "1.0.11", "r2api"),
    ("r2api.skins", "1.4.10", "r2api"),
    ("r2api.addressables", "1.1.3", "r2api"),
    ("r2api.networking", "1.0.4", "r2api"),
    ("r2api.dot", "1.1.2", "r2api"),
    ("r2api.orb", "1.0.1", "r2api"),
    ("r2api.deployable", "1.0.1", "r2api"),
    ("r2api.director", "3.1.1", "r2api"),
]

GAME_DLLS = [
    "RoR2.dll", "Assembly-CSharp.dll", "HGCSharpUtils.dll", "HGUnityUtils.dll", "KinematicCharacterController.dll",
    "LegacyResourcesAPI.dll", "Rewired_Core.dll", "Rewired_CSharp.dll", "Unity.Addressables.dll",
    "Unity.ResourceManager.dll", "Unity.TextMeshPro.dll", "UnityEngine.UI.dll", "com.unity.multiplayer-hlapi.Runtime.dll",
    "AK.Wwise.Unity.API.dll", "AK.Wwise.Unity.API.WwiseTypes.dll", "AK.Wwise.Unity.MonoBehaviour.dll", "Newtonsoft.Json.dll",
]


def fetch(pkg: str, ver: str) -> Path:
    CACHE.mkdir(parents=True, exist_ok=True)
    dest = CACHE / f"{pkg}.{ver}.nupkg"
    if not dest.exists():
        url = f"{FLAT}/{pkg}/{ver}/{pkg}.{ver}.nupkg"
        print(f"  get {pkg} {ver}")
        with urllib.request.urlopen(url) as r:
            dest.write_bytes(r.read())
    return dest


def pick_lib_dir(names):
    """Prefer netstandard2.1 > netstandard2.0 > net45 > lib/ for each package."""
    for tfm in ("lib/netstandard2.1/", "lib/netstandard2.0/", "lib/net472/", "lib/net45/", "lib/"):
        hits = [n for n in names if n.startswith(tfm) and n.endswith(".dll") and "/" not in n[len(tfm):]]
        if hits:
            return hits
    return []


def extract(nupkg: Path, sub: str):
    out = REFS / sub
    out.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(nupkg) as z:
        for name in pick_lib_dir(z.namelist()):
            target = out / Path(name).name
            target.write_bytes(z.read(name))


def from_nuget():
    for pkg, ver, sub in PACKAGES:
        extract(fetch(pkg, ver), sub)
    print("  patching GameLibs 1.3.1 RoR2.dll up to the current DamageSource API (tools/RefTools shim)")
    subprocess.run(["dotnet", "run", "-c", "Release", "--project", str(ROOT / "tools" / "RefTools"), "--",
                    "shim", str(REFS / "game" / "RoR2.dll")], check=True)


def from_game(game: Path, bepinex: Path):
    managed = next(game.glob("*_Data/Managed"), None)
    if managed is None:
        sys.exit(f"no *_Data/Managed under {game}")
    for sub in ("game", "unity"):
        (REFS / sub).mkdir(parents=True, exist_ok=True)
    for dll in managed.glob("*.dll"):
        sub = "unity" if dll.name.startswith("UnityEngine") and dll.name != "UnityEngine.UI.dll" else "game"
        if sub == "game" and dll.name not in GAME_DLLS:
            continue
        shutil.copy2(dll, REFS / sub / dll.name)
    for sub, pattern in (("bepinex", "core/*.dll"), ("mmhook", "plugins/**/MMHOOK_RoR2.dll"),
                         ("r2api", "plugins/**/R2API*.dll"), ("bepinex", "plugins/**/RoR2BepInExPack.dll")):
        (REFS / sub).mkdir(parents=True, exist_ok=True)
        for dll in bepinex.glob(pattern):
            shutil.copy2(dll, REFS / sub / dll.name)
    # MMHOOK may also live in cache/ depending on HookGenPatcher version
    for dll in bepinex.glob("**/MMHOOK_RoR2.dll"):
        shutil.copy2(dll, REFS / "mmhook" / dll.name)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game", type=Path, help="Risk of Rain 2 install folder")
    ap.add_argument("--bepinex", type=Path, help="BepInEx folder with R2API installed (r2modman profile)")
    ap.add_argument("--clean", action="store_true", help="delete refs/ (keeps the download cache)")
    a = ap.parse_args()
    if a.clean:
        for p in REFS.iterdir() if REFS.exists() else []:
            if p.name != ".cache":
                shutil.rmtree(p)
    if a.game:
        if not a.bepinex:
            ap.error("--game needs --bepinex")
        from_game(a.game, a.bepinex)
    else:
        from_nuget()
    (REFS / "refs.json").write_text(json.dumps({"source": "game" if a.game else "nuget.org"}, indent=1))
    print(f"refs ready in {REFS}")


if __name__ == "__main__":
    main()
