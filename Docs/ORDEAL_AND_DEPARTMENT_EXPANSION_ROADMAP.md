# Ordeals & Department Progression Expansion Roadmap (RimWorld 1.7)

> **Status: Early WIP / 极早期不完善版本** — Phases 1–3 and the initial 22-Ordeal animation & VFX pass are functionally implemented and pass 1.7 `WithoutAnomaly` smoke tests, but combat balance, animation polish, VFX tuning, UI presentation, and progression pacing are still very early and subject to major iteration.
> Last updated: 2026-11-23  
> Scope: `LC Anomaly Core` (`DarthCY.LC.AnomalyCore`), `LC-Anomaly-Story` (`DarthCY.LC.AnomalyStory`), `LC-Anomaly-Ordeals` (`DarthCY.LC.AnomalyOrdeals`)  
> Runtime constraints: RimWorld 1.7 / .NET 10, `WithoutAnomaly` compatible (no mandatory Anomaly DLC dependencies), save-compatible `defName`s.

---

## Overall Architecture

This expansion upgrades two coupled progression pillars across the Lobotomy Corporation mod family:

1. **Track A — 10-Department Facility Progression (`LC Anomaly Core` + `LC-Anomaly-Story`)**
   - Elevates the 10 Department Cores (`LC_DepartmentCore_*`) from identical stat-enabling shells into genuinely differentiated department systems with authentic passive employee buffs, room healing/SP restoration, and facility-wide E.G.O / PE-Box bonuses.
   - Completes the full 10-department roster in `LC-Anomaly-Story` (adding Central Command, Disciplinary, and Record departments), binds every department to its `LC_DepartmentCore_*` ThingDef, expands the development tree, and expands department-specific Directives across all 10 departments.
2. **Track B — Ordeals Presentation, Audio & Four-Color Combat (`LC-Anomaly-Ordeals`)**
   - Replaces vanilla tinted placeholder PawnKinds (`Megascarab`, `Scyther`, `Centipede`) with original Lobotomy Corporation Spine-composited textures, authentic Start/End/Combat/Skill `.wav` audio stingers, original four-color resistances (`FourColorTargetExtension`), and authentic four-color damage (`LC_RedDamage` / `LC_WhiteDamage` / `LC_BlackDamage` / `LC_PaleDamage`).

---

## Phase 1 — 10-Department Core Differentiation + Dawn & Noon Overhaul (Completed)

- [x] **Roadmap & Specification**: Establish this phased roadmap across `LC Anomaly Core`, `LC-Anomaly-Story`, and `LC-Anomaly-Ordeals`.
- [x] **Track A1 (`LC Anomaly Core`)**:
  - Fill real EN/ZH descriptions and distinct visual bar colors for all 10 `LC_DepartmentCore_*` ThingDefs.
  - Add `DepartmentCoreUtility` + differentiated per-department employee buffs in `HediffComp_PawnStatus` (refreshed immediately on assignment change):
    - **Control Team (控制部)**: `+15%` Move Speed, `+10%` Global Work Speed.
    - **Information Team (情报部)**: `+25%` Abnormality Study Rate, `+8%` Study Success Rate.
    - **Training Team (培训部)**: `+50%` Four-Status (`Fortitude`/`Prudence`/`Temperance`/`Justice`) work XP gain.
    - **Safety Team (安保部)**: Department Core room healing also restores mental stability (`LC_MentalDamage` / SP) with reduced Enkephalin cost (`10 -> 7`) and `+10%` Injury Healing Factor for assigned staff.
    - **Central Command Team (中央本部)**: 10-employee command hub with balanced `+8%` Move Speed, `+10%` Global Work Speed, `-8%` Incoming Damage Factor.
    - **Welfare Team (福利部)**: `-10%` Incoming Damage Factor, `-12%` Mental Break Threshold, and `+15%` chance for bonus PE-Box production on completed abnormality work.
    - **Disciplinary Team (惩戒部)**: `+15%` Melee Damage Factor, `-12%` Aiming Delay Factor, and **facility-wide E.G.O extraction max cap `+1`** while powered.
    - **Record Team (记录部)**: `+12%` Global Learning Factor, `-15%` Mental Break Threshold, `+20%` Four-Status work XP gain.
    - **Extraction Team (研发部)**: `+15%` Abnormality Study Rate, and **facility-wide E.G.O PE-Box extraction cost `-25%`** (min 1) while powered.
    - **Architecture Team (构筑部)**: Endgame balanced capstone (`+10%` Move Speed, `+10%` Global Work Speed, `+10%` Abnormality Study Rate, `-8%` Incoming Damage Factor).
- [x] **Track A2 (`LC-Anomaly-Story`)**:
  - Complete all 10 departments in `Departments.xml` (preserving all 7 existing `defName`s and adding `LCStory_CentralCommand`, `LCStory_Disciplinary`, `LCStory_Record`), binding `<coreThingDef>` on every department.
  - Add Central Command, Disciplinary, and Record progression nodes in `DevelopmentNodes.xml` (preserving all existing node `defName`s and prerequisites).
  - Track additional company statistics (`goodWorks`, `egoExtracted`, `examinationsPassed`) in `Bootstrap.cs` / `GameComponent_CompanyDevelopment.cs`.
  - Expand `Directives.xml` to 12 themed directives spanning all 10 departments + full EN & ZH translations.
- [x] **Track B (`LC-Anomaly-Ordeals` — Dawn 4 + Noon 4)**:
  - Enable `Cont/` in `LC-Anomaly-Ordeals/LoadFolders.xml`.
  - Render original Spine sprites to `Cont/Textures/Things/Pawn/Ordeals/` for all 8 Dawn & Noon entities: `AmberDawn`, `CrimsonDawn`, `GreenDawn`, `VioletDawn`, `CrimsonNoon`, `GreenNoon`, `VioletNoon`, `IndigoNoon`.
  - Add 34 original `.wav` audio clips under `Cont/Sounds/Ordeals/` and `1.7/Defs/SoundDefs/Ordeal_Sounds.xml` (10 Start/End stingers + 15 spatial Combat/Skill/Dead SFX).
  - Bind authentic original four-color resistances (`FourColorTargetExtension`) to all 8 Dawn & Noon races in `OrdealRaces.xml`.
  - Upgrade Dawn & Noon workers (`DawnOrdealWorker.cs`, `NoonOrdealWorker.cs`, `OrdealPawnPatches.cs`) to deal authentic four-color damage (`LC_RedDamage` / `LC_WhiteDamage` / `LC_BlackDamage`) and play original combat/special SFX.
- [x] **Verification**: Extend `GameplayValidation.cs`, pass `BuildFamily.ps1`, and pass `RuntimeSmokeTest.ps1` (`WithoutAnomaly`) in both `English` and `ChineseSimplified`.

---

## Phase 2 — Manager Tactical Bullets + Dusk (3) & Midnight (3) Ordeals Overhaul (Completed)

- [x] **Manager Tactical Bullets (`LC Anomaly Core`)**:
  - Unlocked by powered **Central Command** (`+4` charges: HP, SP, RED/WHITE/BLACK/PALE Shield Bullets), **Welfare** (`+3` charges: Slowing Bullet), and **Disciplinary** (`+3` charges: Execution Bullet) Department Cores with automatic recharge (`GameComponent_LC`) and authentic Lobotomy Corp icons & `.wav` SFX.
  - Four-Color Shield Bullets (`HediffComp_BulletShield`) integrate cleanly via `ILCPawnDamageInterceptor` in `Patch_Pawn_PreApplyDamage` (50 absorption capacity, hit/break SFX, save-persistent remaining shield).
- [x] **Dusk (3) & Midnight (3 / 6 entities) Original Art, Audio & Mechanics (`LC-Anomaly-Ordeals`)**:
  - Rendered original Spine sprites & 4 color-accented Violet Midnight Shrines (`AmberDusk`, `CrimsonDusk`, `GreenDusk`, `AmberMidnight`, `GreenMidnight`, `VioletMidnightRed/White/Black/Pale`) into `Cont/Textures/Things/Pawn/Ordeals/`.
  - Added 26 original Dusk & Midnight `.wav` audio clips (`Cont/Sounds/Ordeals/`) and 18 spatial `SoundDef`s (`Ordeal_Sounds.xml`), plus wired Dusk/Midnight Start/End presentation stingers.
  - Bound authentic four-color resistances (`FourColorTargetExtension`) to all 9 Dusk & Midnight races in `OrdealRaces.xml` and upgraded Dusk emergence/roll/saw skills to `LC_RedDamage` + authentic SFX.
  - Added **Ordeal Suppression Reward Loop** (`OrdealTargetUtility.GrantOrdealSuppressionLoopRewards`): passing Dawn/Noon/Dusk/Midnight records `ordealsSuppressed`, grants tier-scaled `departmentExperience` to all open departments, and refills Manager Tactical Bullets.

---

## Phase 3 — Sephirah Core Suppression Examinations + White Ordeals Endgame (Completed)

- [x] **Sephirah Core Suppression Examinations (`LC-Anomaly-Story` + `LC Anomaly Core`)**:
  - Added 6 Sephirah Core Suppression Examinations (`LCStory_CoreSuppression_Malkuth`, `Yesod`, `Hod`, `Netzach`, `Gebura`, `Binah`) with distinct active facility constraints (`WorkSuccessPenalty`, `MentalStressPulse`, `ColonistHediff` / `LC_CoreSuppression_HodDebuff`, `HealLockout`) and 6 paired progression nodes (`CoreSuppressionNodes.xml`).
  - Added save-persistent permanent Core Suppression Mastery rewards (`GameComponent_LC.CompleteCoreSuppression` + `DevelopmentReward_CoreSuppression`):
    - **Malkuth (Control)**: All assigned employees permanently `+10%` Move Speed & `+10%` Global Work Speed.
    - **Yesod (Information)**: Permanent `+15%` Abnormality Study Rate & `+25%` bonus PE-Box chance.
    - **Hod (Training)**: Permanent `+50%` four-attribute work XP gain across all departments.
    - **Netzach (Safety)**: All Department Cores permanently gain `-30%` fuel consumption & built-in mental recovery (`>= 0.15`).
    - **Gebura (Disciplinary)**: Permanent facility-wide E.G.O extraction cap `+1` & Max Manager Bullet charges `+2`.
    - **Binah (Extraction)**: Permanent additional `-20%` E.G.O PE-Box extraction cost & faster Manager Bullet recharge (`4000` ticks).
- [x] **White Ordeals (4 Fixers & The Claw) Overhaul (`LC-Anomaly-Ordeals`)**:
  - Rendered all 4 Fixers (`WhiteFixerRed/White/Black/Pale`) and The Claw (`WhiteClaw`, 8-page multi-atlas Spine composite) into `Cont/Textures/Things/Pawn/Ordeals/`.
  - Added 20 original `.wav` clips (`WhiteOrdeal_Start/End`, `Liq_*`, `Claw_*`) and 17 `SoundDef`s (`Ordeal_Sounds.xml`), wired into `WhiteOrdeals.xml`, `WhiteOrdealWorker.cs`, and `OrdealPawnPatches.cs`.
  - Bound authentic four-color resistances (`FourColorTargetExtension`) to all 5 White Ordeal races in `OrdealRaces.xml` and wired tiered endgame rewards (`GrantOrdealSuppressionLoopRewards`, tier 2–5) on pass.

- [x] **Ordeal Animations & Signature Combat VFX Overhaul (`LC-Anomaly-Ordeals`)**:
  - Built native RimWorld 1.7 `PawnRenderTreeDef` (`LCOrdeal_Animated`) with `CompOrdealAnimator` and `PawnRenderNode_OrdealAnimated` supporting multi-keyframe Spine pose switching (`_move1`, `_move2`, `_attack`, `_special`) and 6 procedural secondary-motion styles (`Biped`, `Crawler`, `Float`, `Roller`, `Factory`, `HelixTower`).
  - Rendered 4 multi-directional animation pose sets (`_move1`/`_move2`/`_attack`/`_special`) across all **22 Ordeal entities** from unpacked Lobotomy Corporation Spine rigs + extracted and alpha-calibrated **14 authentic VFX textures** (`Cont/Textures/FX/Ordeals/`: `LaserBeam`, `LaserCore`, `LaserHit`, `SparkMidnight`, `BossPortal`, `HollowRing`, `SoftGlow`, `CraterDecal`, `SlashArc`, `TargetMark`, `TentacleRed`, `TentacleWhite`, `TentacleBlack`, `PaleEye`).
  - Built 60 FPS `MapComponent_OrdealVfx` + `OrdealVfxUtility` rendering real-time beams, shockwaves, portals, tentacles/Pale Eye strikes, lingering white fog lines, lock-on reticles, pawn auras, and 240° death fan sweeps, wired across `DawnOrdealWorker`, `NoonOrdealWorker`, `DuskOrdealWorker`, `MidnightOrdealWorker`, `WhiteOrdealWorker`, and `OrdealPawnPatches`.

---

## Verification Log

- **Phase 1 (2026-11-23)**:
  - `BuildFamily.ps1`: Passed (`7 mods, 5 assemblies, 525 XML files`, 0 warnings / 0 errors across `LCAnomalyCore`, `LCAbnormalities`, `LCAnomalyStory`, `LCAnomalyOrdeals`, `LCAnimationStudio`).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language English`: Passed (`English map passed` & `English load passed`, `[LC17 Validation] GAMEPLAY PASS; ExpansionPhase1; 10_dept_cores/buffs/story_10depts_12directives/dawn_noon_8art_audio_fourcolor`).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified`: Passed (`ChineseSimplified map passed` & `ChineseSimplified load passed`; 0 series translation errors; only 28 verified vanilla 1.7 Chinese warnings).
- **Phase 2 (2026-11-23)**:
  - `BuildFamily.ps1`: Passed (`7 mods, 5 assemblies, 530 XML files`, 0 warnings / 0 errors across all 5 assemblies).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language English`: Passed (`English map passed` & `English load passed`, `[LC17 Validation] GAMEPLAY PASS; ExpansionPhase2; manager_bullets_8types/shield_slow_exec/dusk_midnight_9art_18audio_fourcolor`).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified`: Passed (`ChineseSimplified map passed` & `ChineseSimplified load passed`; 0 series translation errors; only 28 verified vanilla 1.7 Chinese warnings).
- **Phase 3 (2026-11-23)**:
  - `BuildFamily.ps1`: Passed (`7 mods, 5 assemblies, 534 XML files`, 0 warnings / 0 errors across all 5 assemblies).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language English`: Passed (`English map passed` & `English load passed`, `[LC17 Validation] GAMEPLAY PASS; ExpansionPhase3; 6_core_suppressions/mastery_hooks/white_ordeals_5art_17audio_fourcolor` + Core Suppression mastery Scribe save/load persistence check).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified`: Passed (`ChineseSimplified map passed` & `ChineseSimplified load passed`; 0 series translation errors; only 28 verified vanilla 1.7 Chinese warnings).
- **Ordeal Animations & VFX Overhaul (2026-11-23)**:
  - `BuildFamily.ps1`: Passed (`7 mods, 5 assemblies, 535 XML files`, 0 warnings / 0 errors across all 5 assemblies).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language English`: Passed (`English map passed` & `English load passed`, `[LC17 Validation] GAMEPLAY PASS; OrdealAnimAndVfx; 22_ordeals_4poses/render_tree/14_vfx_textures/live_vfx_draw`).
  - `RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified`: Passed (`ChineseSimplified map passed` & `ChineseSimplified load passed`; 0 series translation errors; only 28 verified vanilla 1.7 Chinese warnings).
