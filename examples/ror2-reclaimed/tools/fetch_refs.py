#!/usr/bin/env python3
"""Fetch the reference assemblies Reclaimed compiles against into ./refs (gitignored).

Sources:
  default        R2API modules, MMHOOK.RoR2, RoR2BepInExPack, UnityEngine.Modules, HarmonyX/MonoMod (nuget.org)
                 + RiskOfRain2.GameLibs 1.5.0-r.1 (the live DLC4 build) and BepInEx 5.4.21 (nuget.bepinex.dev).
  --offline-fallback
                 nuget.org only. It carries GameLibs 1.3.1, which predates DamageSource and has older enum
                 values (InterruptPriority, BodyFlags...); `tools/RefTools shim` adds the missing API so the code
                 compiles, but enum constants get baked in with stale values. Never ship that build.
  `tools/RefTools apicheck` then checks a build against the live game's signatures (MMHOOK_RoR2).
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
BEPFEED = "https://nuget.bepinex.dev/v3/package"

# The live game's reference assemblies (DLC1-4) only exist on the BepInEx feed.
GAMELIBS_LIVE = ("riskofrain2.gamelibs", "1.5.0-r.1", "game")
GAMELIBS_FALLBACK = ("riskofrain2.gamelibs", "1.3.1.275-r.0", "game")  # nuget.org; needs the RefTools shim
BEPINEX_BASELIB = ("bepinex.baselib", "5.4.21", "bepinex")  # BepInEx.dll, BepInEx feed

# id, version, sub-folder inside refs/ (all on nuget.org)
PACKAGES = [
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


def fetch(pkg: str, ver: str, feed: str = FLAT) -> Path:
    CACHE.mkdir(parents=True, exist_ok=True)
    dest = CACHE / f"{pkg}.{ver}.nupkg"
    if not dest.exists():
        url = f"{feed}/{pkg}/{ver}/{pkg}.{ver}.nupkg"
        print(f"  get {pkg} {ver}")
        # the BepInEx feed rejects urllib's default User-Agent
        req = urllib.request.Request(url, headers={"User-Agent": "NuGet/6.8 reclaimed-fetch-refs"})
        with urllib.request.urlopen(req) as r:
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


def from_nuget(offline_fallback: bool):
    for pkg, ver, sub in PACKAGES:
        extract(fetch(pkg, ver), sub)
    if not offline_fallback:
        try:
            pkg, ver, sub = GAMELIBS_LIVE
            extract(fetch(pkg, ver, BEPFEED), sub)
            pkg, ver, sub = BEPINEX_BASELIB
            extract(fetch(pkg, ver, BEPFEED), sub)  # overrides the 5.4.22 BepInEx.dll with the RoR2-pinned 5.4.21
            return "nuget.bepinex.dev"
        except OSError as e:
            print(f"  BepInEx feed unreachable ({e}); falling back to GameLibs 1.3.1 + shim")
    pkg, ver, sub = GAMELIBS_FALLBACK
    extract(fetch(pkg, ver), sub)
    print("  patching GameLibs 1.3.1 RoR2.dll up to the current DamageSource API (tools/RefTools shim)")
    subprocess.run(["dotnet", "run", "-c", "Release", "--project", str(ROOT / "tools" / "RefTools"), "--",
                    "shim", str(REFS / "game" / "RoR2.dll")], check=True)
    return "nuget.org+shim"


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
    ap.add_argument("--offline-fallback", action="store_true",
                    help="nuget.org only: GameLibs 1.3.1 + shim (compile checks only; enum values may be stale)")
    a = ap.parse_args()
    if a.clean:
        for p in REFS.iterdir() if REFS.exists() else []:
            if p.name != ".cache":
                shutil.rmtree(p)
    if a.game:
        if not a.bepinex:
            ap.error("--game needs --bepinex")
        from_game(a.game, a.bepinex)
        source = "game"
    else:
        source = from_nuget(a.offline_fallback)
    (REFS / "refs.json").write_text(json.dumps({"source": source}, indent=1))
    print(f"refs ready in {REFS}")


if __name__ == "__main__":
    main()
