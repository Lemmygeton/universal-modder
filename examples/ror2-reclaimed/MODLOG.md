# MODLOG — Reclaimed (Risk of Rain 2)

## 2026-10-11 — setup
- Goal: six bosses as playable survivors, False Son model (boss untouched + purified survivor + boss-earned unlock).
- Route: BepInEx 5 + R2API 5.x plugin (C#, netstandard2.1). No game install in this environment, so no in-game test yet.
- Environment: nuget.org reachable; nuget.bepinex.dev, thunderstore.io, github.com (except raw) blocked.
  - RiskOfRain2.GameLibs on nuget.org is only 1.3.1.275 (has DamageTypeCombo but not DamageSource).
  - tools/RefTools `shim` adds DamageSource to it for compile checks; `apicheck` validates our DLLs against
    MMHOOK_RoR2 2026.10.8 (generated from the live game) — method signatures exact, fields partially.
  - RoR2BepInExPack 1.45.0 (2026-10-10) ships GameAssetPathsBetter: GUIDs for every Addressables asset in the
    live game, DLC3 + DLC4 included. That is our asset source of truth.
- Boss internal names: Mithrix=BrotherBody, Voidling=MiniVoidRaidCrabBodyPhase1-3/VoidRaidCrabBody,
  Solus Wing=SolusWingBody (DLC3), Solus Heart=SolusHeartBody (DLC3), Anima Harvester=ReaperBossBody (DLC4),
  Echo of Providence=ProvidenceMaster (DLC4 root folder; body prefab only reachable through the master).
