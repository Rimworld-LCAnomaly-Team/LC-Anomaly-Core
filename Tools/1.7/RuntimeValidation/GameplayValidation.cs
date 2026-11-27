using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Patch;
using LCAnomalyCore.Render.Weapon;
using LCAnomalyCore.Settings;
using LCAnomalyCore.UI;
using LCAnomalyCore.Util;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LC17Validation;

public static class GameplayValidation
{
    private static readonly Dictionary<Building_AbnormalityContainmentTerminal, CellRect> TestContainmentRoomRects =
        new Dictionary<Building_AbnormalityContainmentTerminal, CellRect>();

    private static IntVec3 FindSafeBuildingCell(Map map)
    {
        return map.AllCells.First(c =>
            c.x >= 8 && c.z >= 8 && c.x < map.Size.x - 8 && c.z < map.Size.z - 8
            && CellRect.CenteredOn(c, 2).Cells.All(p =>
                p.InBounds(map) && !p.Fogged(map) && p.Standable(map) && p.GetRegion(map) != null
                && p.GetEdifice(map) == null && p.GetFirstPawn(map) == null));
    }

    private static Building_AbnormalityContainmentTerminal SpawnTestContainmentTerminal(Map map, bool restrictDoor = false)
    {
        BuildTestContainmentRoom(map, out CellRect roomRect, out Building_LCContainmentDoor door);
        if (door != null)
        {
            door.restrictToEmployees = restrictDoor;
        }

        ThingDef terminalDef = DefDatabase<ThingDef>.GetNamed("LC_ContainmentTerminal", true);
        IntVec3 termPos = new IntVec3(roomRect.minX + 1, 0, roomRect.maxZ - 1);
        var terminal = (Building_AbnormalityContainmentTerminal)ThingMaker.MakeThing(terminalDef);
        terminal.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(terminal, termPos, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        TestContainmentRoomRects[terminal] = roomRect;
        return terminal;
    }

    private static void CleanupTestContainmentTerminal(Building_AbnormalityContainmentTerminal terminal)
    {
        if (terminal == null)
        {
            return;
        }

        Map map = terminal.Map;
        Pawn leftover = terminal.HeldPawn;
        if (!terminal.Destroyed && terminal.Spawned)
        {
            terminal.Destroy();
        }
        else if (!terminal.Destroyed)
        {
            terminal.Destroy();
        }

        if (leftover != null && !leftover.Destroyed)
        {
            if (leftover.Corpse != null && !leftover.Corpse.Destroyed)
            {
                leftover.Corpse.Destroy();
            }
            if (leftover.Spawned)
            {
                leftover.DeSpawn();
            }
            if (!leftover.Destroyed)
            {
                leftover.Destroy();
            }
        }

        if (TestContainmentRoomRects.TryGetValue(terminal, out CellRect rect))
        {
            TestContainmentRoomRects.Remove(terminal);
            if (map != null)
            {
                foreach (IntVec3 c in rect.EdgeCells)
                {
                    List<Thing> things = c.GetThingList(map);
                    for (int i = things.Count - 1; i >= 0; i--)
                    {
                        Thing t = things[i];
                        if ((t is Building_LCContainmentDoor || t.def?.defName == "LC_ContainmentWall") && !t.Destroyed)
                        {
                            t.Destroy();
                        }
                    }
                }

                foreach (IntVec3 c in rect.Cells)
                {
                    map.roofGrid?.SetRoof(c, null);
                }

                map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            }
        }
    }

    private static IntVec3 FindSafePawnCell(Map map, IntVec3 near)
    {
        if (near.InBounds(map) && near.GetRegion(map) != null
            && CellFinder.TryRandomClosewalkCellNear(near, map, 4, out IntVec3 res,
                c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetRegion(map) != null && c.GetFirstBuilding(map) == null && c.GetEdifice(map) == null))
        {
            return res;
        }
        return map.AllCells.First(c => c.x >= 8 && c.z >= 8 && c.x < map.Size.x - 8 && c.z < map.Size.z - 8
            && !c.Fogged(map) && c.Standable(map) && c.GetRegion(map) != null && c.GetFirstBuilding(map) == null && c.GetFirstPawn(map) == null);
    }

    public static void Run(Map map, CompPawnStatus employee)
    {
        bool prevMeltdownEnabled = Setting_LCAnomalyCore_Main.Settings?.If_EnableQliphothMeltdown ?? true;
        bool prevMeltdownOrdealEnabled = Setting_LCAnomalyCore_Main.Settings?.If_EnableMeltdownOrdealTrigger ?? true;
        if (Setting_LCAnomalyCore_Main.Settings != null)
        {
            Setting_LCAnomalyCore_Main.Settings.If_EnableQliphothMeltdown = false;
            Setting_LCAnomalyCore_Main.Settings.If_EnableMeltdownOrdealTrigger = false;
            map?.GetComponent<MapComponent_LCQliphothMeltdown>()?.ResetAllMeltdowns(resetWaveProgress: true);
        }
        try
        {
            RunInternal(map, employee);
        }
        finally
        {
            if (Setting_LCAnomalyCore_Main.Settings != null)
            {
                Setting_LCAnomalyCore_Main.Settings.If_EnableQliphothMeltdown = prevMeltdownEnabled;
                Setting_LCAnomalyCore_Main.Settings.If_EnableMeltdownOrdealTrigger = prevMeltdownOrdealEnabled;
            }
        }
    }

    private static void RunInternal(Map map, CompPawnStatus employee)
    {
        TestContainmentRoomRects.Clear();
        Pawn worker = (Pawn)employee.parent;
        worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
        int index = 0;
        foreach (string name in new[] { "OneSin", "Laetitia", "MeatLantern", "BurrowingHeaven" })
        {
            var platform = SpawnTestContainmentTerminal(map);
            IntVec3 cell = platform.Position;
            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg"));
            int signature = 0x4c430010 + index++;
            AbnormalityUtility.TransferSignature(egg, signature);
            if (!platform.TryCaptureThing(egg))
                throw new InvalidOperationException("Egg failed to enter room containment: " + name);
            Pawn held = platform.HeldPawn ?? throw new InvalidOperationException("Egg failed to enter containment: " + name);
            var abnormality = held.GetComp<CompAbnormality>();
            if (abnormality.biosignature != signature) throw new InvalidOperationException("Identity did not survive containment: " + name);
            AbnormalityUtility.Discover(held.def);
            var entry = DefDatabase<AbnormalityCodexEntryDef>.GetNamedSilentFail(held.def.defName);
            if (entry != null && !entry.Discovered) throw new InvalidOperationException("Codex discovery failed: " + name);
            platform.GetGizmos().ToList();
            held.Drawer.renderer.EnsureGraphicsInitialized();
            held.Drawer.renderer.RenderPawnAt(held.DrawPos);
            if (name == "BurrowingHeaven")
            {
                CompAbnormalityHoldingPlatformTarget bhTarget = held.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
                if (bhTarget == null || bhTarget.Props.heldDrawScale >= 1f || bhTarget.Props.heldDrawScale <= 0f)
                    throw new InvalidOperationException("BurrowingHeaven expected heldDrawScale < 1 inside containment room.");
                platform.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, platform.DrawPos);
                platform.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, platform.DrawPos);
                platform.DynamicDrawPhaseAt(DrawPhase.Draw, platform.DrawPos);
                held.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, held.DrawPos);
                held.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, held.DrawPos);
                held.DynamicDrawPhaseAt(DrawPhase.Draw, held.DrawPos);
            }
            platform.CurWorkType = Enum.GetValues(typeof(EAnomalyWorkType)).Cast<EAnomalyWorkType>()
                .OrderByDescending(w => abnormality.CheckStudierSkillRequire(employee, w)).First();
            platform.Notify_StudyStart(worker);
            int workTicks = name == "MeatLantern" ? 2400 : 1;
            for (int i = 0; i < workTicks; i++) platform.Notify_Studying(worker);
            float successRate = abnormality.CheckStudierSkillRequire(employee, platform.CurWorkType);
            if (successRate <= 0f) throw new InvalidOperationException("Test worker cannot perform work: " + name);
            Rand.PushState(signature);
            try
            {
                for (int i = 0; i < 24; i++) platform.Notify_StudyInterval(employee);
            }
            finally { Rand.PopState(); }
            abnormality.Notify_Studied(worker);
            if (abnormality.PeBoxComp.CurAmountIndiPebox <= 0) throw new InvalidOperationException("No PeBox production: " + name);
            held.GetComp<CompAbnormalityStudiable>().Study(worker, 1000f);
            var ego = held.GetComp<CompEgoExtractable>();
            if (ego != null && ego.Props.weaponExtracted != null)
            {
                abnormality.PeBoxComp.CurAmountIndiPebox = 1000;
                int before = ego.CurAmountWeapon;
                typeof(CompEgoExtractable).GetMethod("ExtractEGO", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ego, new object[] { EGO_TYPE.Weapon });
                if (ego.CurAmountWeapon != before + 1) throw new InvalidOperationException("EGO extraction failed: " + name);
            }
            platform.EjectContents();
            if (!held.Spawned || platform.HeldPawn != null) throw new InvalidOperationException("Release failed: " + name);
            // Exercise the same death-effect and egg-return path invoked by the pawn's kill callback.
            var effect = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Dying" + name), cell, map);
            effect.InitWith(held);
            held.DeSpawn();
            Find.WorldPawns.PassToWorld(held, PawnDiscardDecideMode.Discard);
            effect.Complete();
            Thing returnedEgg = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed(name + "Egg"))
                .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == signature);
            if (returnedEgg == null) throw new InvalidOperationException("Identity did not survive death and egg return: " + name);
            CleanupTestContainmentTerminal(platform);
            Log.Message("[LC17 Validation] GAMEPLAY PASS; " + name + "; capture/work/PeBox/EGO/release/death/identity");
        }
        IntVec3 monsterCell = FindSafePawnCell(map, map.Center);
        var spawnEffect = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("SpawningLaetitiaMonster"), monsterCell, map);
        spawnEffect.GetType().GetMethod("Init").Invoke(spawnEffect, new object[] { -1 });
        int beforeMonsters = map.mapPawns.AllPawnsSpawned.Count(p => p.def.defName == "LaetitiaMonster");
        spawnEffect.Complete();
        Pawn monster = map.mapPawns.AllPawnsSpawned.Last(p => p.def.defName == "LaetitiaMonster");
        if (map.mapPawns.AllPawnsSpawned.Count(p => p.def.defName == "LaetitiaMonster") != beforeMonsters + 1 ||
            monster.Faction != LCContainmentUtility.AbnormalityFaction)
            throw new InvalidOperationException("Monster spawning failed.");
        Pawn target = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        IntVec3 targetCell = monster.Position.GetRegion(map) != null
            && CellFinder.TryRandomClosewalkCellNear(monster.Position, map, 1, out IntVec3 adjTarget,
                c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetRegion(map) != null && c.GetEdifice(map) == null)
            ? adjTarget
            : monster.Position;
        GenSpawn.Spawn(target, targetCell, map);
        float healthBefore = target.health.summaryHealth.SummaryHealthPercent;
        monster.GetType().GetMethod("AttackDirect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(monster, new object[] { target });
        if (target.health.summaryHealth.SummaryHealthPercent >= healthBefore)
            throw new InvalidOperationException("Monster attack caused no damage.");
        Log.Message("[LC17 Validation] GAMEPLAY PASS; LaetitiaMonster; spawn/faction/ability/damage/effects");

        ValidateP0AndP1(map, monster, target);
    }

    private static void ValidateP0AndP1(Map map, Pawn monster, Pawn target)
    {
        // P0: Codex resolution & LC-only dialog construction
        List<AbnormalityCodexEntryDef> codexEntries = DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading;
        if (codexEntries.Count < 4)
            throw new InvalidOperationException($"Expected at least 4 codex entries, found {codexEntries.Count}.");

        foreach (AbnormalityCodexEntryDef entry in codexEntries)
        {
            if (entry.linkedThings == null || entry.linkedThings.Count == 0)
                throw new InvalidOperationException($"Codex entry {entry.defName} has no linkedThings.");

            foreach (ThingDef linked in entry.linkedThings)
            {
                AbnormalityCodexEntryDef resolved = AbnormalityUtility.ResolveCodexEntry(linked);
                if (resolved != entry)
                    throw new InvalidOperationException($"ResolveCodexEntry({linked.defName}) returned {resolved?.defName ?? "null"}, expected {entry.defName}.");
            }

            if (new Dialog_LC_EntityCodex(entry) == null)
                throw new InvalidOperationException($"Failed to instantiate Dialog_LC_EntityCodex for {entry.defName}.");
        }

        if (new Dialog_LC_EntityCodex() == null)
            throw new InvalidOperationException("Failed to instantiate default Dialog_LC_EntityCodex.");

        // P0: ExtractUtil eligibility hook registration/unregistration
        ThingDef oneSinEgg = DefDatabase<ThingDef>.GetNamed("OneSinEgg", true);
        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(oneSinEgg))
            throw new InvalidOperationException("Expected OneSinEgg in ZAYIN ritual extraction list before filtering.");

        Func<Def, bool> tempFilter = def => def != oneSinEgg;
        ExtractUtil.RegisterEligibilityFilter(tempFilter);
        try
        {
            if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(oneSinEgg))
                throw new InvalidOperationException("Eligibility filter failed to exclude OneSinEgg.");
        }
        finally
        {
            ExtractUtil.UnregisterEligibilityFilter(tempFilter);
        }

        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(oneSinEgg))
            throw new InvalidOperationException("UnregisterEligibilityFilter failed to restore OneSinEgg.");

        // P1: Four-color defaults, overrides, and authored E.G.O precedence
        ThingDef powerArmor = DefDatabase<ThingDef>.GetNamed("Apparel_PowerArmor", true);
        if (!FourColorOverrides.TryGetApparelProfile(powerArmor, out FourColorOverrides.ResolvedApparelProfile powerProf)
            || powerProf.source != FourColorProfileSource.DefaultTable
            || powerProf.riskLevel != LCRiskLevel.HE
            || Math.Abs(powerProf.red - 0.8f) > 0.001f)
        {
            throw new InvalidOperationException(
                $"Unexpected Apparel_PowerArmor profile: source={powerProf.source}, risk={powerProf.riskLevel}, red={powerProf.red}.");
        }

        ThingDef basicShirt = DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt", true);
        if (FourColorOverrides.TryGetApparelProfile(basicShirt, out FourColorOverrides.ResolvedApparelProfile shirtProf))
            throw new InvalidOperationException($"Expected Apparel_BasicShirt to stay unconfigured, got {shirtProf.source}.");

        ThingDef bhSuit = DefDatabase<ThingDef>.GetNamed("Apparel_BurrowingHeavenSuit", true);
        if (!FourColorOverrides.TryGetApparelProfile(bhSuit, out FourColorOverrides.ResolvedApparelProfile bhProf)
            || bhProf.source != FourColorProfileSource.Authored
            || bhProf.riskLevel != LCRiskLevel.WAW)
        {
            throw new InvalidOperationException(
                $"Unexpected Apparel_BurrowingHeavenSuit profile: source={bhProf.source}, risk={bhProf.riskLevel}.");
        }

        FourColorOverrides.SetApparelOverride(powerArmor, true, LCRiskLevel.ALEPH, 0.5f, 0.6f, 0.7f, 1.2f);
        try
        {
            if (!FourColorOverrides.TryGetApparelProfile(powerArmor, out FourColorOverrides.ResolvedApparelProfile overrideProf)
                || overrideProf.source != FourColorProfileSource.UserOverride
                || overrideProf.riskLevel != LCRiskLevel.ALEPH
                || Math.Abs(overrideProf.red - 0.5f) > 0.001f)
            {
                throw new InvalidOperationException("SetApparelOverride did not take immediate effect on Apparel_PowerArmor.");
            }
        }
        finally
        {
            FourColorOverrides.RevertApparel(powerArmor.defName);
        }

        if (!FourColorOverrides.TryGetApparelProfile(powerArmor, out FourColorOverrides.ResolvedApparelProfile revertedProf)
            || revertedProf.source != FourColorProfileSource.DefaultTable
            || revertedProf.riskLevel != LCRiskLevel.HE
            || Math.Abs(revertedProf.red - 0.8f) > 0.001f)
        {
            throw new InvalidOperationException("RevertApparel did not restore Apparel_PowerArmor default profile.");
        }

        // P1: Dedicated InfoCard four-color StatDef entries (apparel, weapon, pawn)
        StatRequest powerArmorReq = StatRequest.For(powerArmor, null);
        if (!LCAnomalyCore.Defs.StatDefOf.LC_FourColor_RiskLevel.Worker.ShouldShowFor(powerArmorReq)
            || !LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistRed.Worker.ShouldShowFor(powerArmorReq))
            throw new InvalidOperationException("Apparel_PowerArmor missing dedicated four-color StatDef entries.");

        ThingDef egoOneSinWeapon = DefDatabase<ThingDef>.GetNamed("EgoWeapon_OneSin", true);
        StatRequest oneSinReq = StatRequest.For(egoOneSinWeapon, null);
        if (!LCAnomalyCore.Defs.StatDefOf.LC_FourColor_RiskLevel.Worker.ShouldShowFor(oneSinReq)
            || !LCAnomalyCore.Defs.StatDefOf.LC_FourColor_DamageType.Worker.ShouldShowFor(oneSinReq)
            || !LCAnomalyCore.Defs.StatDefOf.LC_FourColor_DamageMultiplier.Worker.ShouldShowFor(oneSinReq))
            throw new InvalidOperationException("EgoWeapon_OneSin missing dedicated four-color StatDef entries.");

        Pawn victim = (!target.Destroyed && !target.Dead)
            ? target
            : (map.mapPawns.FreeColonistsSpawned.FirstOrDefault() ?? monster);
        StatRequest victimReq = StatRequest.For(victim);
        if (!LCAnomalyCore.Defs.StatDefOf.LC_FourColor_RiskLevel.Worker.ShouldShowFor(victimReq)
            || !LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistRed.Worker.ShouldShowFor(victimReq)
            || !LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistPale.Worker.ShouldShowFor(victimReq))
            throw new InvalidOperationException($"Pawn {victim.def.defName} missing dedicated four-color StatDef entries.");

        // P1: Four-color TakeDamage with both hit icons and damage numbers enabled
        DamageDef redDamage = DefDatabase<DamageDef>.GetNamed("LC_RedDamage", true);
        Setting_LCAnomalyCore settings = Setting_LCAnomalyCore_Main.Settings;
        bool prevIcons = settings != null && settings.If_ShowFourColorHitIcons;
        bool prevNumbers = settings != null && settings.If_ShowFourColorDamageNumbers;
        try
        {
            if (settings != null)
            {
                settings.If_ShowFourColorHitIcons = true;
                settings.If_ShowFourColorDamageNumbers = true;
            }

            if (!victim.Destroyed && !victim.Dead && victim.Spawned)
            {
                victim.TakeDamage(new DamageInfo(redDamage, 2f, instigator: monster));
            }
        }
        finally
        {
            if (settings != null)
            {
                settings.If_ShowFourColorHitIcons = prevIcons;
                settings.If_ShowFourColorDamageNumbers = prevNumbers;
            }
        }

        foreach (string name in new[]
        {
            "OneSin", "Laetitia", "MeatLanternContained", "BurrowingHeaven",
            "PortraitOfAnotherWorld", "FleshIdol", "WeCanChangeAnything", "OldBeliefAndPromise", "NothingThere",
            "FairyFestival", "WellCheers", "PunishingBird", "VoidDream", "HeartOfAspiration",
            "ScorchedGirl", "DerFreischutz", "FuneralOfDeadButterflies", "SnowQueen", "NotesFromCrazedResearcher",
            "AllAroundHelper", "JudgementBird", "BigBird", "LittleRedRidingHoodedMercenary", "BackwardClock", "Schadenfreude"
        })
        {
            if (GraphicUtil.EntityNamePlatformTopGraphic_Get(name) == null)
                throw new InvalidOperationException($"Missing or unresolvable HoldingPlatform name graphic for {name}.");
        }

        ValidateCodexIntelSystem();

        Log.Message("[LC17 Validation] GAMEPLAY PASS; P0/P1; codex/eligibility/fourcolor/infocard/feedback");

        ValidateP2BatchA(map, monster);
    }

    private static void ValidateCodexIntelSystem()
    {
        List<AbnormalityCodexEntryDef> entries = DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading;
        if (entries.Count < 50)
            throw new InvalidOperationException($"Expected >= 50 codex entries with Ordeals loaded, found {entries.Count}.");

        foreach (AbnormalityCodexEntryDef entry in entries)
        {
            if (entry.icon == null || entry.icon == BaseContent.BadTex)
                throw new InvalidOperationException($"Codex entry {entry.defName} failed to load valid icon texture.");
            if (string.IsNullOrEmpty(entry.ResolvedContainmentCode))
                throw new InvalidOperationException($"Codex entry {entry.defName} has empty ResolvedContainmentCode.");
        }

        foreach (string uiTexName in new[]
        {
            "UI/Codex/Codex_WindowBg", "UI/Codex/Codex_HeaderBanner", "UI/Codex/Codex_DossierBanner",
            "UI/Codex/Codex_PortraitFrame", "UI/Codex/Codex_PanelFrame", "UI/Codex/Codex_SectionBar",
            "UI/Codex/Codex_TabNormal", "UI/Codex/Codex_TabActive", "UI/Codex/Codex_TileFrame",
            "UI/Codex/Codex_TileSelected", "UI/Codex/Codex_LockedCard", "UI/Codex/Codex_LockIcon",
            "UI/Codex/Codex_PeBoxIcon", "UI/Codex/Codex_HazardDivider"
        })
        {
            UnityEngine.Texture2D tex = ContentFinder<UnityEngine.Texture2D>.Get(uiTexName, false);
            if (tex == null || tex == BaseContent.BadTex)
                throw new InvalidOperationException($"Missing or unresolvable Codex UI texture: {uiTexName}");
        }

        // Work preference matrix live resolution checks
        ThingDef oneSinDef = DefDatabase<ThingDef>.GetNamed("OneSin", true);
        if (!CodexIntelUtility.TryGetWorkMatrix(oneSinDef, out var oneSinMatrix)
            || Math.Abs(oneSinMatrix[EAnomalyWorkType.Instinct][0] - 0.50f) > 0.001f
            || Math.Abs(oneSinMatrix[EAnomalyWorkType.Insight][0] - 0.70f) > 0.001f)
        {
            throw new InvalidOperationException("OneSin work preference matrix mismatch.");
        }

        ThingDef nothingThereDef = DefDatabase<ThingDef>.GetNamed("NothingThere", true);
        if (!CodexIntelUtility.TryGetWorkMatrix(nothingThereDef, out var ntMatrix)
            || ntMatrix[EAnomalyWorkType.Instinct][0] > 0.001f
            || Math.Abs(ntMatrix[EAnomalyWorkType.Instinct][2] - 0.35f) > 0.001f
            || ntMatrix[EAnomalyWorkType.Insight][0] > 0.001f
            || Math.Abs(ntMatrix[EAnomalyWorkType.Attachment][0] - 0.50f) > 0.001f
            || ntMatrix[EAnomalyWorkType.Repression][0] > 0.001f)
        {
            throw new InvalidOperationException("NothingThere work preference matrix mismatch.");
        }

        ThingDef fdbDef = DefDatabase<ThingDef>.GetNamed("FuneralOfDeadButterflies", true);
        if (!CodexIntelUtility.TryGetWorkMatrix(fdbDef, out var fdbMatrix) || fdbMatrix.Count != 4)
        {
            throw new InvalidOperationException("FuneralOfDeadButterflies work preference matrix missing.");
        }

        GameComponent_LC lc = Components.LC ?? throw new InvalidOperationException("GameComponent_LC missing during Codex validation.");

        // PE-Box intel unlock & observation auto-unlock checks (use LittleRed so OneSin test state is untouched)
        AbnormalityCodexEntryDef lrEntry = AbnormalityUtility.ResolveCodexEntry(DefDatabase<ThingDef>.GetNamed("LittleRedRidingHoodedMercenary", true));
        if (lrEntry == null || lrEntry.ResolvedEntryKind != CodexEntryKind.Abnormality)
            throw new InvalidOperationException("LittleRedRidingHoodedMercenary codex entry resolution failed.");

        AbnormalityUtility.Discover(lrEntry.PrimaryEntityDef);
        lc.TryGetAnomalyStatusSaved(lrEntry.PrimaryEntityDef, out AnomalyStatusSaved saved);
        int peBefore = saved.IndiPeBoxAmount;
        saved.IndiPeBoxAmount += 30;
        lc.AnomalyStatusSavedDict[lrEntry.PrimaryEntityDef] = saved;

        bool unlockedBasic = lc.TryUnlockCodexIntelWithPeBox(lrEntry, CodexIntelSection.BasicInfo, out string failReason);
        if (!unlockedBasic)
            throw new InvalidOperationException($"Expected PE-Box BasicInfo unlock to succeed: {failReason}");

        lc.TryGetAnomalyStatusSaved(lrEntry.PrimaryEntityDef, out saved);
        if (saved.IndiPeBoxAmount != peBefore + 30 - lrEntry.IntelUnlockCost)
            throw new InvalidOperationException($"Unexpected remaining PE-Boxes after Codex unlock: got {saved.IndiPeBoxAmount}, expected {peBefore + 30 - lrEntry.IntelUnlockCost}.");
        if (!lc.HasCodexIntel(lrEntry, CodexIntelSection.BasicInfo))
            throw new InvalidOperationException("LittleRed BasicInfo flag not active after PE-Box purchase.");

        // Ordeal discovery + kill tracking + EscapeCombat unlock
        ThingDef amberDawnRace = DefDatabase<ThingDef>.GetNamed("LCOrdeal_Race_AmberDawn", true);
        AbnormalityCodexEntryDef amberEntry = AbnormalityUtility.ResolveCodexEntry(amberDawnRace);
        if (amberEntry == null || amberEntry.ResolvedEntryKind != CodexEntryKind.Ordeal)
            throw new InvalidOperationException("AmberDawn Ordeal codex entry resolution failed.");

        lc.RecordOrdealKill(amberDawnRace);
        if (!lc.IsAbnormalityDiscovered(amberEntry)
            || lc.GetOrdealKillCount(amberEntry) < 1
            || !lc.HasCodexIntel(amberEntry, CodexIntelSection.EscapeCombat))
        {
            throw new InvalidOperationException("Ordeal kill failed to record kill count and unlock EscapeCombat intel.");
        }

        CodexCompletionSnapshot snap = CodexIntelUtility.GetCompletionSnapshot();
        if (snap.totalEntries < 50 || snap.totalSlots <= 0 || snap.unlockedSlots <= 0)
            throw new InvalidOperationException($"Invalid Codex completion snapshot: total={snap.totalEntries}, slots={snap.unlockedSlots}/{snap.totalSlots}.");

        Log.Message($"[LC17 Validation] CODEX INTEL PASS; entries={snap.totalEntries}; unlocked={snap.unlockedSlots}/{snap.totalSlots}; milestone={snap.milestoneTier}/4");
    }

    private static (Building_AbnormalityContainmentTerminal platform, Pawn held, CompAbnormalityTool tool) ContainToolOnPlatform(
        Map map,
        Pawn testColonist,
        string toolName,
        int signature)
    {
        Building_AbnormalityContainmentTerminal platform = SpawnTestContainmentTerminal(map);

        Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(toolName + "Egg", true));
        AbnormalityUtility.TransferSignature(egg, signature);
        if (!platform.TryCaptureThing(egg))
            throw new InvalidOperationException($"Tool egg failed to enter containment room: {toolName}");

        Pawn held = platform.HeldPawn
            ?? throw new InvalidOperationException($"Tool egg failed to materialize in containment room: {toolName}");
        if (!platform.IsHoldingTool)
            throw new InvalidOperationException($"Containment terminal did not recognize held tool abnormality: {toolName}");

        CompAbnormality abn = held.GetComp<CompAbnormality>()
            ?? throw new InvalidOperationException($"Tool entity missing CompAbnormality: {toolName}");
        if (abn.biosignature != signature)
            throw new InvalidOperationException($"Biosignature mismatch after holding tool: {toolName}");

        if (held.kindDef.canMeleeAttack)
            throw new InvalidOperationException($"Tool entity should have canMeleeAttack=false: {toolName}");

        if (!held.ThreatDisabled(null))
            throw new InvalidOperationException($"Tool entity should be non-aggressive (ThreatDisabled=true): {toolName}");

        if (platform.CompAbnormalityStudiable == null || platform.CompAbnormalityStudiable.CurrentlyStudiable())
            throw new InvalidOperationException($"Tool entity must not be subject to PE-Box work: {toolName}");

        platform.GetGizmos().ToList();
        platform.GetInspectString();
        if (testColonist != null && testColonist.Spawned)
        {
            platform.GetFloatMenuOptions(testColonist).ToList();
        }

        held.Drawer.renderer.EnsureGraphicsInitialized();
        held.Drawer.renderer.RenderPawnAt(held.DrawPos);

        return (platform, held, platform.HeldToolComp);
    }

    private static void ValidateP2BatchA(Map map, Pawn monster)
    {
        // Portrait of Another World (Holding Platform + Egg): extraction pool, Codex, binding, PreApplyDamage redirection, substitute death, eject->egg
        ThingDef portraitDef = DefDatabase<ThingDef>.GetNamed("PortraitOfAnotherWorld", true);
        ThingDef portraitEggDef = DefDatabase<ThingDef>.GetNamed("PortraitOfAnotherWorldEgg", true);
        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("HE").Contains(portraitEggDef))
            throw new InvalidOperationException("PortraitOfAnotherWorldEgg missing from HE ritual extraction list.");

        if (AbnormalityUtility.ResolveCodexEntry(portraitDef)?.defName != "PortraitOfAnotherWorld"
            || AbnormalityUtility.ResolveCodexEntry(portraitEggDef)?.defName != "PortraitOfAnotherWorld")
            throw new InvalidOperationException("PortraitOfAnotherWorld or its egg failed Codex resolution.");

        HediffDef portraitProtected = DefDatabase<HediffDef>.GetNamed("LC_PortraitProtected", true);
        HediffDef portraitSubstitute = DefDatabase<HediffDef>.GetNamed("LC_PortraitSubstitute", true);

        Pawn ward = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(ward, FindSafePawnCell(map, map.Center), map);

        const int ejectSig = 0x4c430081;
        var (portraitPlatform, heldPortrait, tool) = ContainToolOnPlatform(map, ward, "PortraitOfAnotherWorld", ejectSig);
        if (!tool.CanUseTool(ward).Accepted)
            throw new InvalidOperationException("Colonist should be able to bind PortraitOfAnotherWorld on platform.");

        ShaderDef portraitShaderDef = DefDatabase<ShaderDef>.GetNamedSilentFail("LC_PortraitOfAnotherWorld");
        if (portraitShaderDef == null || portraitShaderDef.Shader == null)
            throw new InvalidOperationException("LC_PortraitOfAnotherWorld ShaderDef failed to resolve or compile.");

        tool.OnToolUsed(ward);
        if (tool.UseCount != 1 || tool.StudyUnlocksComp == null || tool.StudyUnlocksComp.Progress < 1)
            throw new InvalidOperationException("PortraitOfAnotherWorld failed to record tool use or unlock StudyNote I.");

        portraitPlatform.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, portraitPlatform.DrawPos);
        portraitPlatform.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, portraitPlatform.DrawPos);
        portraitPlatform.DynamicDrawPhaseAt(DrawPhase.Draw, portraitPlatform.DrawPos);

        PropertyInfo propSub = tool.GetType().GetProperty("SubstitutePawn")
            ?? throw new InvalidOperationException("Missing SubstitutePawn property on CompPortraitOfAnotherWorld.");
        PropertyInfo propCrimson = tool.GetType().GetProperty("CrimsonStained")
            ?? throw new InvalidOperationException("Missing CrimsonStained property on CompPortraitOfAnotherWorld.");
        PropertyInfo propRedirected = tool.GetType().GetProperty("TotalDamageRedirected")
            ?? throw new InvalidOperationException("Missing TotalDamageRedirected property.");
        PropertyInfo propIsBound = tool.GetType().GetProperty("IsBound")
            ?? throw new InvalidOperationException("Missing IsBound property on CompPortraitOfAnotherWorld.");

        Pawn sub = propSub.GetValue(tool) as Pawn;
        if (sub == null || sub == ward)
            throw new InvalidOperationException("PortraitOfAnotherWorld failed to bind a distinct substitute pawn.");

        if (!ward.health.hediffSet.HasHediff(portraitProtected) || !sub.health.hediffSet.HasHediff(portraitSubstitute))
            throw new InvalidOperationException("Portrait bond Hediffs missing after binding.");

        float wardHpBefore = ward.health.summaryHealth.SummaryHealthPercent;
        float subHpBefore = sub.health.summaryHealth.SummaryHealthPercent;
        DamageDef redDamage = DefDatabase<DamageDef>.GetNamed("LC_RedDamage", true);
        ward.TakeDamage(new DamageInfo(redDamage, 10f, armorPenetration: 1f, instigator: monster));

        if (Math.Abs(ward.health.summaryHealth.SummaryHealthPercent - wardHpBefore) > 0.0001f)
            throw new InvalidOperationException("PortraitOfAnotherWorld failed to absorb incoming damage on protected ward.");

        if (sub.health.summaryHealth.SummaryHealthPercent >= subHpBefore)
            throw new InvalidOperationException("PortraitOfAnotherWorld failed to transfer damage to substitute pawn.");

        if ((int)propRedirected.GetValue(tool) <= 0)
            throw new InvalidOperationException("PortraitOfAnotherWorld TotalDamageRedirected remained zero.");

        if (!sub.Dead) sub.Kill(null);
        if (!(bool)propCrimson.GetValue(tool))
            throw new InvalidOperationException("PortraitOfAnotherWorld did not stain crimson after substitute death.");

        portraitPlatform.DynamicDrawPhaseAt(DrawPhase.Draw, portraitPlatform.DrawPos);

        // Eject from holding platform: clears bond immediately and reverts pawn to Egg on tick without corpse/hostility
        portraitPlatform.EjectContents();
        if (ward.health.hediffSet.HasHediff(portraitProtected))
            throw new InvalidOperationException("PortraitOfAnotherWorld failed to clear bond Hediff when leaving platform.");

        for (int i = 0; i < 3; i++) Find.TickManager.DoSingleTick();
        if (!heldPortrait.Destroyed && heldPortrait.Spawned)
            throw new InvalidOperationException("PortraitOfAnotherWorld pawn did not revert/despawn after leaving holding platform.");

        Thing returnedPortraitEgg = map.listerThings.ThingsOfDef(portraitEggDef)
            .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == ejectSig);
        if (returnedPortraitEgg == null)
            throw new InvalidOperationException("PortraitOfAnotherWorld did not revert to matching biosignature egg after leaving containment room.");
        CleanupTestContainmentTerminal(portraitPlatform);

        // Contain a second bound Portrait on a holding platform to verify Scribe_References survival across save/reload
        const int savedPortraitSig = 0x4c430082;
        Pawn savedWard = SpawnValidationColonist(map, FindFarthestSafeCell(map));
        if (savedWard.drafter != null) savedWard.drafter.Drafted = true;
        var (_, _, savedTool) = ContainToolOnPlatform(map, savedWard, "PortraitOfAnotherWorld", savedPortraitSig);
        savedTool.OnToolUsed(savedWard);
        if (!(bool)propIsBound.GetValue(savedTool))
            throw new InvalidOperationException("Saved PortraitOfAnotherWorld failed to enter bound state on holding platform.");

        // SevenSin Entities: active check, extract list, Codex, four-color weapons & race resistances
        if (!ExpansionActive.SevenSinEntityActive)
            throw new InvalidOperationException("ExpansionActive.SevenSinEntityActive returned false with LC Abnormalities active.");

        var sevenSinList = ExtractUtil.Get_AnomlayLvl2DefList_SevenSin("ZAYIN");
        foreach (string sinName in new[] { "GluttonyEntity", "LustEntity", "MelancholyEntity", "PrideEntity", "SlothEntity", "WrathEntity" })
        {
            ThingDef sinThing = DefDatabase<ThingDef>.GetNamed(sinName, true);
            PawnKindDef sinKind = DefDatabase<PawnKindDef>.GetNamed(sinName, true);
            if (!sevenSinList.Contains(sinKind))
                throw new InvalidOperationException($"SevenSin extraction list missing {sinName}.");
            if (AbnormalityUtility.ResolveCodexEntry(sinThing)?.defName != "SevenSinEntity_ZAYIN")
                throw new InvalidOperationException($"Codex resolution failed for {sinName}.");
        }

        ThingDef melDef = DefDatabase<ThingDef>.GetNamed("MelancholyEntity", true);
        if (!FourColorOverrides.TryGetWeaponProfile(melDef, out FourColorOverrides.ResolvedWeaponProfile melWp)
            || melWp.damageType != FourColorDamageType.White)
            throw new InvalidOperationException("MelancholyEntity expected White four-color weapon profile.");

        ThingDef gluDef = DefDatabase<ThingDef>.GetNamed("GluttonyEntity", true);
        if (!FourColorOverrides.TryGetWeaponProfile(gluDef, out FourColorOverrides.ResolvedWeaponProfile gluWp)
            || gluWp.damageType != FourColorDamageType.Black)
            throw new InvalidOperationException("GluttonyEntity expected Black four-color weapon profile.");

        ThingDef sloDef = DefDatabase<ThingDef>.GetNamed("SlothEntity", true);
        FourColorOverrides.ResolvedRaceProfile sloRp = FourColorOverrides.GetRaceProfile(sloDef);
        if (sloRp.targetKind != FourColorTargetKind.Abnormality || Math.Abs(sloRp.red - 0.6f) > 0.001f)
            throw new InvalidOperationException($"Unexpected SlothEntity race profile: kind={sloRp.targetKind}, red={sloRp.red}.");

        // Verify LCAbnormalities settings toggles filter Portrait egg and SevenSin via ExtractUtil
        Type settingsHolder = GenTypes.GetTypeInAnyAssembly("LCAbnormalities.Settings.LCAbnormalitiesMod")
            ?? throw new InvalidOperationException("LCAbnormalities.Settings.LCAbnormalitiesMod type not found.");
        object abnSettings = settingsHolder.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("LCAbnormalitiesMod.Settings is null.");
        FieldInfo fPortrait = abnSettings.GetType().GetField("EnablePortraitOfAnotherWorld");
        FieldInfo fSevenSin = abnSettings.GetType().GetField("EnableSevenSinEntities");
        bool prevPortrait = (bool)fPortrait.GetValue(abnSettings);
        bool prevSevenSin = (bool)fSevenSin.GetValue(abnSettings);
        try
        {
            fPortrait.SetValue(abnSettings, false);
            fSevenSin.SetValue(abnSettings, false);
            if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual("HE").Contains(portraitEggDef))
                throw new InvalidOperationException("EnablePortraitOfAnotherWorld=false did not filter PortraitOfAnotherWorldEgg.");
            if (ExtractUtil.Get_AnomlayLvl2DefList_SevenSin("ZAYIN").Any())
                throw new InvalidOperationException("EnableSevenSinEntities=false did not filter SevenSin entities.");
        }
        finally
        {
            fPortrait.SetValue(abnSettings, prevPortrait);
            fSevenSin.SetValue(abnSettings, prevSevenSin);
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; P2A; platform_tool/portrait/redirection/eject_egg/sevensin/fourcolor");

        ValidateP2BatchB(map, monster, abnSettings);
    }

    private static void ValidateP2BatchB(Map map, Pawn monster, object abnSettings)
    {
        // 1. Flesh Idol (T-09-79, WAW Continuous Tool on Holding Platform)
        ThingDef fleshDef = DefDatabase<ThingDef>.GetNamed("FleshIdol", true);
        ThingDef fleshEggDef = DefDatabase<ThingDef>.GetNamed("FleshIdolEgg", true);
        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("WAW").Contains(fleshEggDef))
            throw new InvalidOperationException("FleshIdolEgg missing from WAW ritual extraction list.");
        if (AbnormalityUtility.ResolveCodexEntry(fleshDef)?.defName != "FleshIdol"
            || AbnormalityUtility.ResolveCodexEntry(fleshEggDef)?.defName != "FleshIdol")
            throw new InvalidOperationException("FleshIdol or its egg failed Codex resolution.");

        Pawn prayUser = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f));
        GenSpawn.Spawn(prayUser, FindSafePawnCell(map, map.Center), map);
        Hediff prayWound = HediffMaker.MakeHediff(RimWorld.DamageDefOf.Cut.hediff, prayUser, prayUser.RaceProps.body.corePart);
        prayWound.Severity = 8f;
        prayUser.health.AddHediff(prayWound);
        float prayHpWithInjury = prayUser.health.summaryHealth.SummaryHealthPercent;

        const int fleshSig = 0x4c430083;
        var (fleshPlatform, heldFlesh, fleshTool) = ContainToolOnPlatform(map, prayUser, "FleshIdol", fleshSig);

        fleshTool.OnContinuousStart(prayUser);
        for (int t = 60; t <= 1980; t += 60)
        {
            fleshTool.OnContinuousTick(prayUser, 60, t);
        }
        int fleshPulses = (int)fleshTool.GetType().GetProperty("HealPulsesTriggered").GetValue(fleshTool);
        if (fleshPulses <= 0)
            throw new InvalidOperationException("FleshIdol failed to trigger colony heal pulse inside 20-80s window.");
        if (prayUser.health.summaryHealth.SummaryHealthPercent <= prayHpWithInjury)
            throw new InvalidOperationException("FleshIdol heal pulse did not heal injured colonist.");
        if (fleshTool.StudyUnlocksComp == null || fleshTool.StudyUnlocksComp.Progress < 1)
            throw new InvalidOperationException("FleshIdol failed to unlock StudyNote I after 33s prayer.");
        fleshTool.OnContinuousEnd(prayUser, 1980, completedNaturally: true);

        Pawn impatientUser = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f));
        GenSpawn.Spawn(impatientUser, FindSafePawnCell(map, fleshPlatform.Position), map);
        float impHpBefore = impatientUser.health.summaryHealth.SummaryHealthPercent;
        int impMissingBefore = impatientUser.health.hediffSet.GetMissingPartsCommonAncestors().Count;
        fleshTool.OnContinuousStart(impatientUser);
        fleshTool.OnContinuousTick(impatientUser, 60, 300);
        fleshTool.OnContinuousEnd(impatientUser, 300, completedNaturally: true);
        float mentalSev = impatientUser.health.hediffSet.GetFirstHediffOfDef(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage)?.Severity ?? 0f;
        int impMissingAfter = impatientUser.health.hediffSet.GetMissingPartsCommonAncestors().Count;
        bool punishedHealth = impatientUser.Dead
            || impatientUser.health.summaryHealth.SummaryHealthPercent < impHpBefore
            || impMissingAfter > impMissingBefore;
        if (!punishedHealth || (!impatientUser.Dead && mentalSev <= 0.1f))
        {
            string hediffsStr = string.Join(",", impatientUser.health.hediffSet.hediffs.Select(h => $"{h.def.defName}:{h.Severity:0.##}"));
            string apparelStr = string.Join(",", impatientUser.apparel?.WornApparel?.Select(a => a.def.defName) ?? Array.Empty<string>());
            DamageDef blackDef = DefDatabase<DamageDef>.GetNamed("LC_BlackDamage", false);
            throw new InvalidOperationException(
                $"FleshIdol failed to punish premature <20s prayer termination. " +
                $"hpBefore={impHpBefore:0.###}, hpAfter={impatientUser.health.summaryHealth.SummaryHealthPercent:0.###}, " +
                $"missing={impMissingBefore}->{impMissingAfter}, mental={mentalSev:0.###}, dead={impatientUser.Dead}, " +
                $"useCount={fleshTool.UseCount}, blackDef={blackDef?.defName ?? "NULL"}, hediffs=[{hediffsStr}], apparel=[{apparelStr}].");
        }

        // Eject Flesh Idol from containment -> verifies peaceful reversion to Egg
        fleshPlatform.EjectContents();
        for (int i = 0; i < 3; i++) Find.TickManager.DoSingleTick();
        if (!heldFlesh.Destroyed && heldFlesh.Spawned)
            throw new InvalidOperationException("FleshIdol did not despawn after leaving containment.");
        if (!map.listerThings.ThingsOfDef(fleshEggDef).Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == fleshSig))
            throw new InvalidOperationException("FleshIdol did not revert to egg with matching biosignature.");
        CleanupTestContainmentTerminal(fleshPlatform);

        // 2. We Can Change Anything (T-09-85, ZAYIN Continuous Tool on Holding Platform)
        ThingDef ironDef = DefDatabase<ThingDef>.GetNamed("WeCanChangeAnything", true);
        ThingDef ironEggDef = DefDatabase<ThingDef>.GetNamed("WeCanChangeAnythingEgg", true);
        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(ironEggDef))
            throw new InvalidOperationException("WeCanChangeAnythingEgg missing from ZAYIN ritual extraction list.");
        if (AbnormalityUtility.ResolveCodexEntry(ironDef)?.defName != "WeCanChangeAnything"
            || AbnormalityUtility.ResolveCodexEntry(ironEggDef)?.defName != "WeCanChangeAnything")
            throw new InvalidOperationException("WeCanChangeAnything or its egg failed Codex resolution.");

        Pawn ironSubject = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f,
            allowDowned: false,
            allowAddictions: false,
            validatorPreGear: p => p.story?.traits == null || !p.story.traits.allTraits.Any(t => t.def.defName == "Wimp")));
        TraitDef toughTrait = DefDatabase<TraitDef>.GetNamed("Tough", true);
        ironSubject.story?.traits?.allTraits?.Clear();
        ironSubject.story?.traits?.GainTrait(new Trait(toughTrait));
        GenSpawn.Spawn(ironSubject, FindSafePawnCell(map, map.Center), map);

        const int ironSig = 0x4c430084;
        var (ironPlatform, heldIron, ironTool) = ContainToolOnPlatform(map, ironSubject, "WeCanChangeAnything", ironSig);
        if (ironTool.CanManuallyStopContinuous(prayUser))
            throw new InvalidOperationException("WeCanChangeAnything should disallow manual cancel by default.");

        ThingDef boxDef = DefDatabase<ThingDef>.GetNamed("EnkephalinBox", true);
        int boxesBefore = map.listerThings.ThingsOfDef(boxDef).Sum(t => t.stackCount);

        if (!ironTool.TryAcceptEnclosedUser(ironSubject))
            throw new InvalidOperationException("WeCanChangeAnything failed to enclose colonist inside its chamber.");
        if (ironSubject.Spawned || !ironTool.HasEnclosedOccupant || ironTool.EnclosedOccupant != ironSubject || !ironTool.IsActiveContinuous)
            throw new InvalidOperationException("Enclosed WeCanChangeAnything occupant should be hidden from map and held internally.");

        foreach (string ironAnimName in new[]
        {
            "WeCanChangeAnything_Default_First",
            "WeCanChangeAnything_Default_After",
            "WeCanChangeAnything_Start_First",
            "WeCanChangeAnything_Start_After",
            "WeCanChangeAnything_Generate_01",
            "WeCanChangeAnything_Generate_02",
            "WeCanChangeAnything_Generate_03",
            "WeCanChangeAnything_End"
        })
        {
            if (DefDatabase<AnimationDef>.GetNamed(ironAnimName, true) == null)
                throw new InvalidOperationException($"Missing AnimationDef {ironAnimName}.");
        }

        if (heldIron.RaceProps.renderTree?.defName != "WeCanChangeAnything")
            throw new InvalidOperationException($"WeCanChangeAnything expected renderTree WeCanChangeAnything, got {heldIron.RaceProps.renderTree?.defName}.");
        if (heldIron.Drawer.renderer.CurAnimation?.defName != "WeCanChangeAnything_Start_First")
            throw new InvalidOperationException($"WeCanChangeAnything expected Start_First animation on enclosure, got {heldIron.Drawer.renderer.CurAnimation?.defName}.");

        ironPlatform.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, ironPlatform.DrawPos);
        ironPlatform.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, ironPlatform.DrawPos);
        ironPlatform.DynamicDrawPhaseAt(DrawPhase.Draw, ironPlatform.DrawPos);

        LC_EntityBasePawn heldIronEntity = (LC_EntityBasePawn)heldIron;
        for (int t = 0; t < 65; t++)
        {
            heldIronEntity.TickHolded();
        }
        if (heldIron.Drawer.renderer.CurAnimation?.defName != "WeCanChangeAnything_Generate_01")
            throw new InvalidOperationException($"WeCanChangeAnything expected Generate_01 shaking animation after closing, got {heldIron.Drawer.renderer.CurAnimation?.defName}.");

        for (int t = 0; t < 655; t++)
        {
            heldIronEntity.TickHolded();
        }
        if (ironTool.IsActiveContinuous && heldIron.Drawer.renderer.CurAnimation?.defName != "WeCanChangeAnything_Generate_02")
            throw new InvalidOperationException($"WeCanChangeAnything expected Generate_02 shaking animation after 10s, got {heldIron.Drawer.renderer.CurAnimation?.defName}.");

        ironPlatform.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, ironPlatform.DrawPos);
        ironPlatform.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, ironPlatform.DrawPos);
        ironPlatform.DynamicDrawPhaseAt(DrawPhase.Draw, ironPlatform.DrawPos);

        int boxesAfter = map.listerThings.ThingsOfDef(boxDef).Sum(t => t.stackCount);
        int boxesRecorded = (int)ironTool.GetType().GetProperty("TotalBoxesProduced").GetValue(ironTool);
        if (boxesAfter <= boxesBefore || boxesRecorded <= 0)
            throw new InvalidOperationException("WeCanChangeAnything did not produce EnkephalinBox outputs.");
        if (ironTool.StudyUnlocksComp == null || ironTool.StudyUnlocksComp.Progress < 1)
            throw new InvalidOperationException("WeCanChangeAnything failed to unlock StudyNote I after 12s use.");

        // Continue grinding until fatal completion -> occupant dies, corpse drops to platform, active state resets
        for (int t = 0; t < 3600 && !ironSubject.Dead; t++)
        {
            heldIronEntity.TickHolded();
        }
        if (!ironSubject.Dead || ironTool.HasEnclosedOccupant || ironTool.IsActiveContinuous)
            throw new InvalidOperationException("WeCanChangeAnything did not fatally finish enclosed occupant or release container.");
        if (heldIron.Drawer.renderer.CurAnimation?.defName != "WeCanChangeAnything_End")
            throw new InvalidOperationException($"WeCanChangeAnything expected End opening animation upon victim death, got {heldIron.Drawer.renderer.CurAnimation?.defName}.");
        for (int t = 0; t < 75; t++)
        {
            heldIronEntity.TickHolded();
        }
        if (heldIron.Drawer.renderer.CurAnimation?.defName != "WeCanChangeAnything_Default_After")
            throw new InvalidOperationException($"WeCanChangeAnything expected Default_After bloody idle animation after opening, got {heldIron.Drawer.renderer.CurAnimation?.defName}.");
        if (ironSubject.Corpse == null || !ironSubject.Corpse.Spawned)
            throw new InvalidOperationException("WeCanChangeAnything did not eject occupant corpse onto map upon death.");
        int victimsRecorded = (int)ironTool.GetType().GetProperty("TotalVictims").GetValue(ironTool);
        bool bloodStained = (bool)ironTool.GetType().GetProperty("StainedWithBlood").GetValue(ironTool);
        if (victimsRecorded < 1 || !bloodStained)
            throw new InvalidOperationException("WeCanChangeAnything did not record victim death / blood stain.");

        // Safety-ejection check: ejecting platform while an occupant is enclosed safely returns the living colonist before egg reversion
        Pawn ironSurvivor = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f,
            allowDowned: false,
            allowAddictions: false,
            validatorPreGear: p => p.story?.traits == null || !p.story.traits.allTraits.Any(t => t.def.defName == "Wimp")));
        GenSpawn.Spawn(ironSurvivor, FindSafePawnCell(map, ironPlatform.Position), map);
        if (!ironTool.TryAcceptEnclosedUser(ironSurvivor) || ironSurvivor.Spawned || !ironTool.HasEnclosedOccupant)
            throw new InvalidOperationException("WeCanChangeAnything failed second enclosure for safety-ejection check.");
        ironPlatform.EjectContents();
        if (!ironSurvivor.Spawned || ironSurvivor.Dead || ironTool.HasEnclosedOccupant)
            throw new InvalidOperationException("Platform eject failed to safely expel enclosed WeCanChangeAnything occupant.");
        for (int i = 0; i < 3; i++) Find.TickManager.DoSingleTick();
        if (!map.listerThings.ThingsOfDef(ironEggDef).Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == ironSig))
            throw new InvalidOperationException("WeCanChangeAnything did not revert to matching biosignature egg after eject.");
        CleanupTestContainmentTerminal(ironPlatform);

        // 3. Old Belief and Promise (T-09-97, ZAYIN SingleUse Tool + Weapon Blessing in Containment Room)
        ThingDef promiseDef = DefDatabase<ThingDef>.GetNamed("OldBeliefAndPromise", true);
        ThingDef promiseEggDef = DefDatabase<ThingDef>.GetNamed("OldBeliefAndPromiseEgg", true);
        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(promiseEggDef))
            throw new InvalidOperationException("OldBeliefAndPromiseEgg missing from ZAYIN ritual extraction list.");
        if (AbnormalityUtility.ResolveCodexEntry(promiseDef)?.defName != "OldBeliefAndPromise"
            || AbnormalityUtility.ResolveCodexEntry(promiseEggDef)?.defName != "OldBeliefAndPromise")
            throw new InvalidOperationException("OldBeliefAndPromise or its egg failed Codex resolution.");

        Pawn blessedWielder = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f));
        GenSpawn.Spawn(blessedWielder, FindFarthestSafeCell(map), map);
        if (blessedWielder.drafter != null) blessedWielder.drafter.Drafted = true;
        blessedWielder.equipment?.DestroyAllEquipment();

        const int promiseSig = 0x4c430085;
        var (promisePlat, _, promiseTool) = ContainToolOnPlatform(map, blessedWielder, "OldBeliefAndPromise", promiseSig);
        if (promiseTool.CanUseTool(blessedWielder).Accepted)
            throw new InvalidOperationException("OldBeliefAndPromise should reject unarmed colonist.");

        ThingWithComps egoWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_OneSin", true));
        blessedWielder.equipment.AddEquipment(egoWeapon);
        DamageDef whiteDef = DefDatabase<DamageDef>.GetNamed("LC_WhiteDamage", true);
        float unblessedDamage = DamageUtils.GetAdjustedDamage(
            new DamageInfo(whiteDef, 20f, armorPenetration: 1f, instigator: blessedWielder, weapon: egoWeapon.def),
            monster,
            FourColorDamageType.White,
            LCRiskLevel.ZAYIN);

        // Pick a deterministic seed where Rand.Chance(0.20f) evaluates to false (success)
        int successSeed = 1;
        for (int candidate = 1; candidate <= 50; candidate++)
        {
            Rand.PushState(candidate);
            bool betrayed = Rand.Chance(0.20f);
            Rand.PopState();
            if (!betrayed)
            {
                successSeed = candidate;
                break;
            }
        }

        Rand.PushState(successSeed);
        try
        {
            promiseTool.OnToolUsed(blessedWielder);
        }
        finally
        {
            Rand.PopState();
        }

        GameComponent_LC lcComp = GameComponent_LC.Instance ?? throw new InvalidOperationException("GameComponent_LC.Instance is null.");
        if (!lcComp.IsWeaponBlessed(egoWeapon))
            throw new InvalidOperationException("OldBeliefAndPromise did not register blessed weapon in GameComponent_LC.");

        float blessedMult = lcComp.GetWeaponBlessingMultiplier(egoWeapon);
        if (Math.Abs(blessedMult - 1.25f) > 0.01f)
            throw new InvalidOperationException($"Unexpected OldBelief tier-1 blessing multiplier: {blessedMult}");

        float blessedDamage = DamageUtils.GetAdjustedDamage(
            new DamageInfo(whiteDef, 20f, armorPenetration: 1f, instigator: blessedWielder, weapon: egoWeapon.def),
            monster,
            FourColorDamageType.White,
            LCRiskLevel.ZAYIN);
        if (blessedDamage < unblessedDamage * 1.23f)
            throw new InvalidOperationException($"Weapon blessing did not scale DamageUtils.GetAdjustedDamage ({unblessedDamage} -> {blessedDamage}).");

        string blessingLabel = "LC_Stat_WeaponBlessing".Translate();
        if (!egoWeapon.def.SpecialDisplayStats(StatRequest.For(egoWeapon)).Any(s => s.LabelCap == blessingLabel.CapitalizeFirst()))
            throw new InvalidOperationException("InfoCard SpecialDisplayStats missing LC_Stat_WeaponBlessing entry for blessed weapon.");
        CleanupTestContainmentTerminal(promisePlat);

        // 4. Verify extraction pool settings toggles for all 3 tool eggs
        FieldInfo fFlesh = abnSettings.GetType().GetField("EnableFleshIdol");
        FieldInfo fIron = abnSettings.GetType().GetField("EnableWeCanChangeAnything");
        FieldInfo fPromise = abnSettings.GetType().GetField("EnableOldBeliefAndPromise");
        bool prevFlesh = (bool)fFlesh.GetValue(abnSettings);
        bool prevIron = (bool)fIron.GetValue(abnSettings);
        bool prevPromise = (bool)fPromise.GetValue(abnSettings);
        try
        {
            fFlesh.SetValue(abnSettings, false);
            fIron.SetValue(abnSettings, false);
            fPromise.SetValue(abnSettings, false);
            if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual("WAW").Contains(fleshEggDef))
                throw new InvalidOperationException("EnableFleshIdol=false did not filter FleshIdolEgg.");
            if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(ironEggDef))
                throw new InvalidOperationException("EnableWeCanChangeAnything=false did not filter WeCanChangeAnythingEgg.");
            if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ZAYIN").Contains(promiseEggDef))
                throw new InvalidOperationException("EnableOldBeliefAndPromise=false did not filter OldBeliefAndPromiseEgg.");
        }
        finally
        {
            fFlesh.SetValue(abnSettings, prevFlesh);
            fIron.SetValue(abnSettings, prevIron);
            fPromise.SetValue(abnSettings, prevPromise);
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; P2B; platform_continuous/fleshidol/ironmaiden/oldbelief/blessing");

        ValidateBurrowingHeavenTrophyHeads(map);
        ValidateNothingThere(map, abnSettings);

        // Save/Load check: keep one enclosed living colonist inside WeCanChangeAnything across save/reload (takes only 120 pre-save + 120 post-load ticks)
        Pawn ironSavedOccupant = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f,
            allowDowned: false,
            allowAddictions: false,
            validatorPreGear: p => p.story?.traits == null || !p.story.traits.allTraits.Any(t => t.def.defName == "Wimp")));
        ironSavedOccupant.story?.traits?.allTraits?.Clear();
        ironSavedOccupant.story?.traits?.GainTrait(new Trait(toughTrait));
        GenSpawn.Spawn(ironSavedOccupant, FindSafePawnCell(map, map.Center), map);
        const int ironSavedSig = 0x4c430086;
        var (_, _, ironSavedTool) = ContainToolOnPlatform(map, ironSavedOccupant, "WeCanChangeAnything", ironSavedSig);
        if (!ironSavedTool.TryAcceptEnclosedUser(ironSavedOccupant) || ironSavedOccupant.Spawned || !ironSavedTool.HasEnclosedOccupant)
            throw new InvalidOperationException("Saved WeCanChangeAnything failed to enclose colonist.");
    }

    private static void ValidateBurrowingHeavenTrophyHeads(Map map)
    {
        const int bhTrophySig = 0x4c430089;
        var bhPlatform = SpawnTestContainmentTerminal(map);
        IntVec3 bhCell = bhPlatform.Position;

        Thing bhEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("BurrowingHeavenEgg"));
        AbnormalityUtility.TransferSignature(bhEgg, bhTrophySig);
        if (!bhPlatform.TryCaptureThing(bhEgg))
            throw new InvalidOperationException("BurrowingHeavenEgg failed to enter trophy test room containment.");
        Pawn heldBh = bhPlatform.HeldPawn ?? throw new InvalidOperationException("BurrowingHeavenEgg failed to enter trophy test containment.");

        Assembly lcAbnAsm = heldBh.GetType().Assembly;
        Type recordType = lcAbnAsm.GetType("BurrowingHeaven.Things.HangingHeadRecord", throwOnError: true);
        Type compType = lcAbnAsm.GetType("BurrowingHeaven.Comp.CompBurrowingHeaven", throwOnError: true);
        MethodInfo createFrom = recordType.GetMethod("CreateFrom", BindingFlags.Public | BindingFlags.Static);
        FieldInfo capturedLayersField = recordType.GetField("capturedLayers", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo hasUsableProp = recordType.GetProperty("HasUsableCapturedLayers", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo getCompositeMethod = recordType.GetMethod("GetCompositeLayers", BindingFlags.Public | BindingFlags.Instance);

        ThingComp bhComp = heldBh.AllComps.FirstOrDefault(c => compType.IsInstanceOfType(c))
            ?? throw new InvalidOperationException("CompBurrowingHeaven missing on held BurrowingHeaven.");
        FieldInfo hangingHeadsField = compType.GetField("hangingHeads", BindingFlags.Instance | BindingFlags.NonPublic);
        IList hangingHeads = (IList)hangingHeadsField.GetValue(bhComp);
        if (hangingHeads == null)
        {
            hangingHeads = (IList)Activator.CreateInstance(hangingHeadsField.FieldType);
            hangingHeadsField.SetValue(bhComp, hangingHeads);
        }

        // Slot 0: Vanilla humanlike colonist with explicit hair
        HairDef vanillaHair = DefDatabase<HairDef>.AllDefsListForReading
            .FirstOrDefault(h => h != null && !h.noGraphic && !h.texPath.NullOrEmpty())
            ?? throw new InvalidOperationException("No non-empty HairDef found for trophy head validation.");
        BeardDef vanillaBeard = DefDatabase<BeardDef>.AllDefsListForReading
            .FirstOrDefault(b => b != null && !b.noGraphic && !b.texPath.NullOrEmpty());

        Pawn vanillaVictim = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            fixedGender: Gender.Male,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f));
        vanillaVictim.story.hairDef = vanillaHair;
        if (vanillaBeard != null && vanillaVictim.style != null)
        {
            vanillaVictim.style.beardDef = vanillaBeard;
        }
        GenSpawn.Spawn(vanillaVictim, FindSafePawnCell(map, bhCell), map);
        vanillaVictim.Drawer?.renderer?.renderTree?.SetDirty();

        object vanillaRec = createFrom.Invoke(null, new object[] { vanillaVictim, 0, -18f });
        IList vanillaCaptured = (IList)capturedLayersField.GetValue(vanillaRec);
        if (vanillaCaptured == null || vanillaCaptured.Count < 2)
            throw new InvalidOperationException($"Vanilla trophy capture expected >= 2 layers, got {vanillaCaptured?.Count ?? 0}.");

        bool hasVanillaHairLayer = false;
        foreach (object layerObj in vanillaCaptured)
        {
            string texPath = (string)layerObj.GetType().GetField("texPath").GetValue(layerObj);
            if (texPath == vanillaHair.texPath)
            {
                hasVanillaHairLayer = true;
                break;
            }
        }
        if (!hasVanillaHairLayer)
            throw new InvalidOperationException($"Vanilla trophy capture missing HairDef texPath '{vanillaHair.texPath}'.");

        if (!(bool)hasUsableProp.GetValue(vanillaRec))
            throw new InvalidOperationException("Vanilla trophy HasUsableCapturedLayers returned false.");
        IList vanillaComposite = (IList)getCompositeMethod.Invoke(vanillaRec, null);
        if (vanillaComposite == null || vanillaComposite.Count < 3)
            throw new InvalidOperationException($"Vanilla trophy composite expected >= 3 layers (with NeckBlood), got {vanillaComposite?.Count ?? 0}.");

        hangingHeads.Add(vanillaRec);

        // Slot 1 (when HAR Monolyn Race is active): Monolyn colonist wearing hood hiding back hair
        Pawn monolynVictim = null;
        PawnKindDef monolynKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Monolyn_WandererPlayer");
        if (monolynKind != null)
        {
            monolynVictim = PawnGenerator.GeneratePawn(monolynKind, Faction.OfPlayer);
            GenSpawn.Spawn(monolynVictim, FindSafePawnCell(map, bhCell), map);

            ThingDef hoodDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_MNHatHood");
            if (hoodDef != null && monolynVictim.apparel != null)
            {
                ThingDef hoodStuff = GenStuff.DefaultStuffFor(hoodDef);
                if (ThingMaker.MakeThing(hoodDef, hoodStuff) is Apparel hood)
                {
                    monolynVictim.apparel.Wear(hood, dropReplacedApparel: false);
                }
            }

            monolynVictim.Drawer?.renderer?.renderTree?.SetDirty();
            object monolynRec = createFrom.Invoke(null, new object[] { monolynVictim, 1, 16f });
            IList monoCaptured = (IList)capturedLayersField.GetValue(monolynRec);
            if (monoCaptured == null || monoCaptured.Count < 3)
                throw new InvalidOperationException($"Monolyn trophy capture expected >= 3 layers, got {monoCaptured?.Count ?? 0}.");

            var layerSummary = new List<string>();
            float? hairBY = null;
            float? faceHeadY = null;
            float? hairFY = null;
            foreach (object layerObj in monoCaptured)
            {
                Type lt = layerObj.GetType();
                string texPath = (string)lt.GetField("texPath").GetValue(layerObj);
                float relY = (float)lt.GetField("relativeY").GetValue(layerObj);
                Vector2 offXZ = (Vector2)lt.GetField("localOffsetXZ").GetValue(layerObj);
                Vector2 scaleXZ = (Vector2)lt.GetField("localScale").GetValue(layerObj);
                Vector2 meshSz = (Vector2)lt.GetField("meshSize").GetValue(layerObj);
                layerSummary.Add($"{texPath}(y={relY:F4},off={offXZ.x:F3},{offXZ.y:F3},sc={scaleXZ.x:F2},{scaleXZ.y:F2},mesh={meshSz.x:F2}x{meshSz.y:F2})");
                if (texPath != null)
                {
                    if (texPath.IndexOf("HairB", StringComparison.OrdinalIgnoreCase) >= 0) hairBY = relY;
                    if (texPath.IndexOf("HairF", StringComparison.OrdinalIgnoreCase) >= 0) hairFY = relY;
                    if (texPath.IndexOf("MonolynHead", StringComparison.OrdinalIgnoreCase) >= 0) faceHeadY = relY;
                }
            }

            if (!hairFY.HasValue || !hairBY.HasValue || !faceHeadY.HasValue)
                throw new InvalidOperationException($"Monolyn trophy capture missing HairF/HairB/MonolynHead. Captured: {string.Join("; ", layerSummary)}");
            if (!(hairBY.Value < faceHeadY.Value && faceHeadY.Value < hairFY.Value))
                throw new InvalidOperationException($"Unexpected Monolyn layer ordering HairB={hairBY} Face={faceHeadY} HairF={hairFY}");
            if (!(bool)hasUsableProp.GetValue(monolynRec))
                throw new InvalidOperationException("Monolyn trophy HasUsableCapturedLayers returned false.");

            hangingHeads.Add(monolynRec);
            Log.Message($"[LC17 Validation] MONOLYN TROPHY PASS; count={monoCaptured.Count}; layers={string.Join("; ", layerSummary)}");
        }

        heldBh.Drawer?.renderer?.renderTree?.SetDirty();
        bhPlatform.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, bhPlatform.DrawPos);
        bhPlatform.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, bhPlatform.DrawPos);
        bhPlatform.DynamicDrawPhaseAt(DrawPhase.Draw, bhPlatform.DrawPos);

        if (!vanillaVictim.Destroyed && vanillaVictim.Spawned) { vanillaVictim.DeSpawn(); vanillaVictim.Destroy(); }
        if (monolynVictim != null && !monolynVictim.Destroyed && monolynVictim.Spawned) { monolynVictim.DeSpawn(); monolynVictim.Destroy(); }

        Log.Message($"[LC17 Validation] GAMEPLAY PASS; BurrowingHeavenTrophy; vanillaLayers={vanillaCaptured.Count}; monolynTested={monolynKind != null}");
    }

    private static void ValidateNothingThere(Map map, object abnSettings)
    {
        ThingDef ntDef = DefDatabase<ThingDef>.GetNamed("NothingThere", true);
        ThingDef ntEggDef = DefDatabase<ThingDef>.GetNamed("NothingThereEgg", true);
        if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ALEPH").Contains(ntEggDef))
            throw new InvalidOperationException("NothingThereEgg missing from ALEPH ritual extraction list.");

        FieldInfo fNt = abnSettings.GetType().GetField("EnableNothingThere")
            ?? throw new InvalidOperationException("Missing EnableNothingThere field on Setting_LCAbnormalities.");
        bool prevNt = (bool)fNt.GetValue(abnSettings);
        try
        {
            fNt.SetValue(abnSettings, false);
            if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual("ALEPH").Contains(ntEggDef))
                throw new InvalidOperationException("EnableNothingThere=false did not filter NothingThereEgg.");
        }
        finally
        {
            fNt.SetValue(abnSettings, prevNt);
        }

        if (AbnormalityUtility.ResolveCodexEntry(ntDef)?.defName != "NothingThere"
            || AbnormalityUtility.ResolveCodexEntry(ntEggDef)?.defName != "NothingThere")
            throw new InvalidOperationException("NothingThere or its egg failed Codex resolution.");

        // 1. Contain NothingThereEgg and verify room/render/biosignature
        const int ntSig = 0x4c430090;
        var ntPlat = SpawnTestContainmentTerminal(map);
        IntVec3 ntCell = ntPlat.Position;

        Thing ntEgg = ThingMaker.MakeThing(ntEggDef);
        AbnormalityUtility.TransferSignature(ntEgg, ntSig);
        if (!ntPlat.TryCaptureThing(ntEgg))
            throw new InvalidOperationException("NothingThereEgg failed to enter room containment.");

        Pawn heldNt = ntPlat.HeldPawn ?? throw new InvalidOperationException("NothingThereEgg failed to materialize in containment room.");
        CompAbnormality abnNt = heldNt.GetComp<CompAbnormality>()
            ?? throw new InvalidOperationException("NothingThere missing CompAbnormality.");
        if (abnNt.biosignature != ntSig)
            throw new InvalidOperationException($"NothingThere biosignature mismatch: expected {ntSig}, got {abnNt.biosignature}.");

        CompAbnormalityHoldingPlatformTarget ntHoldTarget = heldNt.GetComp<CompAbnormalityHoldingPlatformTarget>()
            ?? throw new InvalidOperationException("NothingThere missing CompAbnormalityHoldingPlatformTarget.");
        if (!ntHoldTarget.Props.hasAnimation)
            throw new InvalidOperationException("NothingThere CompProperties_AbnormalityHoldingPlatformTarget.hasAnimation must be true.");

        foreach (string animName in new[]
        {
            "NothingThere_DogIdle", "NothingThere_DogWork", "NothingThere_DogWalk", "NothingThere_DogAttack",
            "NothingThere_DisguiseIdle", "NothingThere_DisguiseWalk",
            "NothingThere_CocoonIdle",
            "NothingThere_TrueIdle", "NothingThere_TrueWalk", "NothingThere_TrueMelee", "NothingThere_TrueThrow", "NothingThere_TrueGoodbye"
        })
        {
            if (DefDatabase<AnimationDef>.GetNamed(animName, true) == null)
                throw new InvalidOperationException($"Missing AnimationDef {animName}.");
        }

        MethodInfo mTickHolded = heldNt.GetType().GetMethod("TickHolded", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("NothingThere missing public TickHolded method.");
        mTickHolded.Invoke(heldNt, null);
        if (heldNt.Drawer.renderer.CurAnimation?.defName != "NothingThere_DogIdle")
            throw new InvalidOperationException($"Contained NothingThere expected NothingThere_DogIdle animation, got {heldNt.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");

        ntPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.Draw, ntPlat.DrawPos);

        abnNt.Notify_Studying(null);
        mTickHolded.Invoke(heldNt, null);
        if (heldNt.Drawer.renderer.CurAnimation?.defName != "NothingThere_DogWork")
            throw new InvalidOperationException($"Studying NothingThere expected NothingThere_DogWork animation, got {heldNt.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
        ntPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.Draw, ntPlat.DrawPos);

        heldNt.Drawer.renderer.EnsureGraphicsInitialized();
        heldNt.Drawer.renderer.RenderPawnAt(ntPlat.DrawPos);

        // 2. Level V veteran colonist work rates, PE-Box production, study unlock, and Mimicry E.G.O extraction
        Pawn veteran = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f,
            allowDowned: false,
            allowAddictions: false,
            validatorPreGear: p => p.story?.traits == null || !p.story.traits.allTraits.Any(t => t.def.defName == "Wimp")));
        GenSpawn.Spawn(veteran, FindSafePawnCell(map, ntCell), map);
        CompPawnStatus vetStatus = veteran.GetComp<CompPawnStatus>()
            ?? throw new InvalidOperationException("Veteran colonist missing CompPawnStatus.");
        vetStatus.SetPawnStatusEnabled(true, null);
        vetStatus.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 100;
        vetStatus.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 100;
        vetStatus.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 100;
        vetStatus.GetPawnStatusLevel(EPawnStatus.Justice).Status = 100;

        float insightRate = abnNt.CheckStudierSkillRequire(vetStatus, EAnomalyWorkType.Insight);
        float repressionRate = abnNt.CheckStudierSkillRequire(vetStatus, EAnomalyWorkType.Repression);
        float attachRate = abnNt.CheckStudierSkillRequire(vetStatus, EAnomalyWorkType.Attachment);
        if (insightRate > 0.001f || repressionRate > 0.001f || attachRate < 0.50f)
            throw new InvalidOperationException($"Unexpected NothingThere work rates for Level V: insight={insightRate}, repression={repressionRate}, attach={attachRate}.");

        int passSeed = 1;
        while (passSeed <= 1000)
        {
            Rand.PushState(passSeed);
            bool pass = Rand.Chance(attachRate);
            Rand.PopState();
            if (pass)
                break;
            passSeed++;
        }

        ntPlat.CurWorkType = EAnomalyWorkType.Attachment;
        ntPlat.Notify_StudyStart(veteran);
        for (int i = 0; i < 33; i++)
        {
            Rand.PushState(passSeed);
            try
            {
                ntPlat.Notify_StudyInterval(vetStatus);
            }
            finally
            {
                Rand.PopState();
            }
        }
        abnNt.Notify_Studied(veteran, false);

        if (veteran.Dead)
            throw new InvalidOperationException("Level V veteran colonist died unexpectedly during successful Attachment work on NothingThere.");
        if (abnNt.PeBoxComp.CurAmountIndiPebox <= 0)
            throw new InvalidOperationException("NothingThere produced 0 PE-Boxes after Level V Attachment work.");

        heldNt.GetComp<CompAbnormalityStudiable>().Study(veteran, 1000f);
        if (abnNt.StudyUnlocksComp == null || !abnNt.StudyUnlocksComp.Completed)
            throw new InvalidOperationException("NothingThere failed to complete study unlocks.");

        CompEgoExtractable ego = heldNt.GetComp<CompEgoExtractable>()
            ?? throw new InvalidOperationException("NothingThere missing CompEgoExtractable.");
        abnNt.PeBoxComp.CurAmountIndiPebox = 1000;
        MethodInfo extractMethod = typeof(CompEgoExtractable).GetMethod("ExtractEGO", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CompEgoExtractable.ExtractEGO method not found.");
        int beforeWeapon = ego.CurAmountWeapon;
        int beforeArmor = ego.CurAmountArmor;
        extractMethod.Invoke(ego, new object[] { EGO_TYPE.Weapon });
        extractMethod.Invoke(ego, new object[] { EGO_TYPE.Armor });
        if (ego.CurAmountWeapon != beforeWeapon + 1 || ego.CurAmountArmor != beforeArmor + 1)
            throw new InvalidOperationException($"NothingThere EGO extraction count mismatch: weapon={ego.CurAmountWeapon}, armor={ego.CurAmountArmor}.");

        ThingDef mimicryWeaponDef = DefDatabase<ThingDef>.GetNamed("EgoWeapon_NothingThere", true);
        ThingDef mimicrySuitDef = DefDatabase<ThingDef>.GetNamed("Apparel_NothingThereSuit", true);
        ThingWithComps mimicryWeapon = map.listerThings.ThingsOfDef(mimicryWeaponDef).OfType<ThingWithComps>().LastOrDefault()
            ?? throw new InvalidOperationException("Extracted EgoWeapon_NothingThere not found on map.");
        Apparel mimicrySuit = map.listerThings.ThingsOfDef(mimicrySuitDef).OfType<Apparel>().LastOrDefault()
            ?? throw new InvalidOperationException("Extracted Apparel_NothingThereSuit not found on map.");

        // 3. Mimicry E.G.O four-color profiles, cellular suit regen & unequip cleanup
        if (!FourColorOverrides.TryGetApparelProfile(mimicrySuitDef, out FourColorOverrides.ResolvedApparelProfile suitProf)
            || suitProf.source != FourColorProfileSource.Authored
            || suitProf.riskLevel != LCRiskLevel.ALEPH
            || Math.Abs(suitProf.red - 0.2f) > 0.001f
            || Math.Abs(suitProf.white - 0.5f) > 0.001f
            || Math.Abs(suitProf.black - 0.5f) > 0.001f
            || Math.Abs(suitProf.pale - 1.0f) > 0.001f)
        {
            throw new InvalidOperationException($"Unexpected Apparel_NothingThereSuit four-color profile: {suitProf.riskLevel}, R={suitProf.red}, W={suitProf.white}, B={suitProf.black}, P={suitProf.pale}.");
        }

        if (!FourColorOverrides.TryGetWeaponProfile(mimicryWeaponDef, out FourColorOverrides.ResolvedWeaponProfile wepProf)
            || wepProf.source != FourColorProfileSource.Authored
            || wepProf.riskLevel != LCRiskLevel.ALEPH
            || wepProf.damageType != FourColorDamageType.Red)
        {
            throw new InvalidOperationException($"Unexpected EgoWeapon_NothingThere four-color profile: {wepProf.riskLevel}, {wepProf.damageType}.");
        }

        HediffDef regenDef = DefDatabase<HediffDef>.GetNamed("LC_MimicrySuitRegen", true);
        veteran.apparel?.DestroyAll();
        veteran.equipment?.DestroyAllEquipment();
        if (mimicrySuit.Spawned) mimicrySuit.DeSpawn();
        veteran.apparel.Wear(mimicrySuit, dropReplacedApparel: false);
        if (!veteran.health.hediffSet.HasHediff(regenDef))
            throw new InvalidOperationException("Equipping Apparel_NothingThereSuit did not grant LC_MimicrySuitRegen hediff.");

        DamageInfo testWound = new DamageInfo(RimWorld.DamageDefOf.Cut, 8f, 2f, -1f, null, veteran.RaceProps.body.corePart);
        testWound.SetIgnoreArmor(true);
        veteran.TakeDamage(testWound);
        float hpBeforeRegen = veteran.health.summaryHealth.SummaryHealthPercent;
        if (hpBeforeRegen >= 0.999f)
            throw new InvalidOperationException("Test cut failed to injure Mimicry suit wearer before regen test.");

        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        for (int i = 0; i < 200; i++) Find.TickManager.DoSingleTick();
        if (veteran.health.summaryHealth.SummaryHealthPercent <= hpBeforeRegen)
            throw new InvalidOperationException("LC_MimicrySuitRegen failed to heal wearer over 200 ticks.");

        veteran.apparel.Remove(mimicrySuit);
        if (veteran.health.hediffSet.HasHediff(regenDef))
            throw new InvalidOperationException("Unequipping Apparel_NothingThereSuit did not remove LC_MimicrySuitRegen hediff.");
        if (mimicrySuit.Spawned) mimicrySuit.DeSpawn();
        veteran.apparel.Wear(mimicrySuit, dropReplacedApparel: false);
        if (!veteran.health.hediffSet.HasHediff(regenDef))
            throw new InvalidOperationException("Re-equipping Apparel_NothingThereSuit failed to re-apply LC_MimicrySuitRegen.");

        // 4. Mimicry weapon melee Red lifesteal & Mimicry_Goodbye active ability
        if (mimicryWeapon.Spawned) mimicryWeapon.DeSpawn();
        veteran.equipment.AddEquipment(mimicryWeapon);

        PawnKindDef animalKind = DefDatabase<PawnKindDef>.AllDefsListForReading
            .FirstOrDefault(k => k.RaceProps?.Animal == true && !k.RaceProps.IsMechanoid && k.RaceProps.baseBodySize >= 1.5f)
            ?? DefDatabase<PawnKindDef>.AllDefsListForReading.First(k => k.RaceProps?.Animal == true && !k.RaceProps.IsMechanoid);

        Pawn target1 = PawnGenerator.GeneratePawn(animalKind, null);
        GenSpawn.Spawn(target1, FindSafePawnCell(map, veteran.Position), map);
        DamageInfo preLifeWound = new DamageInfo(RimWorld.DamageDefOf.Cut, 10f, 2f, -1f, null, veteran.RaceProps.body.corePart);
        preLifeWound.SetIgnoreArmor(true);
        veteran.TakeDamage(preLifeWound);
        float hpBeforeLife = veteran.health.summaryHealth.SummaryHealthPercent;
        DamageDef lifestealDef = DefDatabase<DamageDef>.GetNamed("LC_Mimicry_RedLifesteal", true);
        target1.TakeDamage(new DamageInfo(lifestealDef, 28f, 0.6f, instigator: veteran, weapon: mimicryWeaponDef));
        if (veteran.health.summaryHealth.SummaryHealthPercent <= hpBeforeLife)
            throw new InvalidOperationException("LC_Mimicry_RedLifesteal melee hit did not heal wielder.");

        Pawn target2 = PawnGenerator.GeneratePawn(animalKind, null);
        GenSpawn.Spawn(target2, FindSafePawnCell(map, veteran.Position), map);
        DamageInfo preGoodbyeWound = new DamageInfo(RimWorld.DamageDefOf.Cut, 12f, 2f, -1f, null, veteran.RaceProps.body.corePart);
        preGoodbyeWound.SetIgnoreArmor(true);
        veteran.TakeDamage(preGoodbyeWound);
        float hpBeforeGoodbye = veteran.health.summaryHealth.SummaryHealthPercent;

        Ability goodbyeAbility = mimicryWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
            ?? throw new InvalidOperationException("EgoWeapon_NothingThere missing Mimicry_Goodbye AbilityForReading.");
        if (goodbyeAbility.def.defName != "Mimicry_Goodbye")
            throw new InvalidOperationException($"Unexpected EgoWeapon_NothingThere ability: {goodbyeAbility.def.defName}.");
        int target2MissingBefore = target2.health.hediffSet.hediffs.Count(h => h is Hediff_MissingPart);
        goodbyeAbility.Activate(new LocalTargetInfo(target2), LocalTargetInfo.Invalid);
        bool target2Damaged = target2.Dead
            || target2.health.summaryHealth.SummaryHealthPercent < 0.999f
            || target2.health.hediffSet.hediffs.Count(h => h is Hediff_MissingPart) > target2MissingBefore;
        if (!target2Damaged)
            throw new InvalidOperationException($"Mimicry_Goodbye ability dealt no damage to target (kind={animalKind.defName}, hediffs={target2.health.hediffSet.hediffs.Count}, casterDead={veteran.Dead}).");
        if (veteran.health.summaryHealth.SummaryHealthPercent <= hpBeforeGoodbye)
            throw new InvalidOperationException("Mimicry_Goodbye ability did not lifesteal-heal wielder.");

        if (!target1.Destroyed && target1.Spawned) { target1.DeSpawn(); target1.Destroy(); }
        if (!target2.Destroyed && target2.Spawned) { target2.DeSpawn(); target2.Destroy(); }

        // 5. Wiki Rules I/II/IV/V/VI + 3 Forms + Left/Right Facing + Doubled Skill Durations + Regen + Egg Return
        Type ntCompType = abnNt.GetType();
        PropertyInfo propForm = ntCompType.GetProperty("CurrentForm", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propHasDisguise = ntCompType.GetProperty("HasDisguise", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propIsPerformingGoodbye = ntCompType.GetProperty("IsPerformingGoodbye", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propIsThrowingBoneSpike = ntCompType.GetProperty("IsThrowingBoneSpike", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propGoodbyeWindup = ntCompType.GetProperty("GoodbyeWindupTicks", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propGoodbyeSlash = ntCompType.GetProperty("GoodbyeSlashTicks", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo propLastDamaged = ntCompType.GetProperty("LastDamagedTick", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo mToForm2 = ntCompType.GetMethod("TransitionToForm2", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo mToForm3 = ntCompType.GetMethod("TransitionToForm3", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo mStartThrow = ntCompType.GetMethod("StartBoneSpikeThrow", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo mStartGoodbye = ntCompType.GetMethod("StartGoodbyeWindup", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo mTickBreachCombat = ntCompType.GetMethod("TickBreachCombat", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CompNothingThere missing TickBreachCombat.");

        // 5a. Wiki Rule II test in temporary undisguised room: Justice < IV breaches immediately WITHOUT killing worker (Dog form East & West)
        Thing rule2Egg = ThingMaker.MakeThing(ntEggDef);
        var rule2Plat = SpawnTestContainmentTerminal(map);
        IntVec3 rule2Cell = rule2Plat.Position;
        if (!rule2Plat.TryCaptureThing(rule2Egg))
            throw new InvalidOperationException("NothingThere rule2Egg failed to enter room containment.");
        Pawn rule2Nt = rule2Plat.HeldPawn;
        CompAbnormality abnRule2 = rule2Nt.GetComp<CompAbnormality>();

        Pawn lowJustWorker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
            canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
        GenSpawn.Spawn(lowJustWorker, FindSafePawnCell(map, rule2Cell), map);
        CompPawnStatus lowJustStatus = lowJustWorker.GetComp<CompPawnStatus>();
        lowJustStatus.SetPawnStatusEnabled(true, null);
        lowJustStatus.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 50; // Lv III -> 0.6x scaling
        lowJustStatus.GetPawnStatusLevel(EPawnStatus.Justice).Status = 20;   // Lv I (< IV)
        float scaledRate = abnRule2.CheckStudierSkillRequire(lowJustStatus, EAnomalyWorkType.Attachment);
        if (scaledRate >= attachRate)
            throw new InvalidOperationException($"Rule I Fortitude scaling did not reduce work rate for Lv III Fortitude: lv3={scaledRate}, lv5={attachRate}.");

        rule2Plat.CurWorkType = EAnomalyWorkType.Attachment;
        rule2Plat.Notify_StudyStart(lowJustWorker);
        abnRule2.Notify_Studied(lowJustWorker, false);
        if (lowJustWorker.Dead)
            throw new InvalidOperationException("Wiki Rule II violated: Justice < IV worker was killed instead of direct breach.");
        if (!abnRule2.Escaped || !rule2Nt.Spawned || (bool)propHasDisguise.GetValue(abnRule2))
            throw new InvalidOperationException("Wiki Rule II failed: undisguised Dog did not breach on Justice < IV completion.");
        rule2Nt.Drawer.renderer.EnsureGraphicsInitialized();
        rule2Nt.Rotation = Rot4.East;
        rule2Nt.Drawer.renderer.RenderPawnAt(rule2Nt.DrawPos);
        rule2Nt.Rotation = Rot4.West;
        rule2Nt.Drawer.renderer.RenderPawnAt(rule2Nt.DrawPos);
        if (!lowJustWorker.Destroyed && lowJustWorker.Spawned) { lowJustWorker.DeSpawn(); lowJustWorker.Destroy(); }
        CleanupTestContainmentTerminal(rule2Plat);
        if (!rule2Nt.Destroyed && rule2Nt.Spawned) { rule2Nt.DeSpawn(); rule2Nt.Destroy(); }

        // 5b. Wiki Rule IV: Bad result with Justice >= IV kills worker & enters Contained Disguised state (without breaching)
        Pawn victimWorker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
            canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
        GenSpawn.Spawn(victimWorker, FindSafePawnCell(map, ntCell), map);
        CompPawnStatus victimStatus = victimWorker.GetComp<CompPawnStatus>()
            ?? throw new InvalidOperationException("Justice IV worker missing CompPawnStatus.");
        victimStatus.SetPawnStatusEnabled(true, null);
        victimStatus.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 80;
        victimStatus.GetPawnStatusLevel(EPawnStatus.Justice).Status = 80;

        ntPlat.CurWorkType = EAnomalyWorkType.Attachment;
        ntPlat.Notify_StudyStart(victimWorker);
        abnNt.Notify_Studied(victimWorker, false); // 0 PE-Boxes -> Bad result
        if (!victimWorker.Dead)
            throw new InvalidOperationException("Wiki Rule IV failed: Bad result with Justice >= IV did not kill worker.");
        if (abnNt.Escaped || !(bool)propHasDisguise.GetValue(abnNt) || Convert.ToInt32(propForm.GetValue(abnNt)) != 0)
            throw new InvalidOperationException("Wiki Rule IV failed: NothingThere did not enter Contained Disguised state inside unit.");
        PropertyInfo propDisguiseMat = abnNt.GetType().GetProperty("CachedDisguiseMaterial", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CompNothingThere missing CachedDisguiseMaterial property.");
        if (propDisguiseMat.GetValue(abnNt) == null)
            throw new InvalidOperationException("CompNothingThere.CachedDisguiseMaterial was null after KillAndStealSkinContained.");
        ntPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.Draw, ntPlat.DrawPos);
        if (victimWorker.Corpse != null && victimWorker.Corpse.Spawned)
        {
            victimWorker.Corpse.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, victimWorker.Corpse.DrawPos);
            victimWorker.Corpse.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, victimWorker.Corpse.DrawPos);
            victimWorker.Corpse.DynamicDrawPhaseAt(DrawPhase.Draw, victimWorker.Corpse.DrawPos);
        }

        // 5c. Wiki Rules V & VI: Low-Fortitude worker enters Contained Disguised unit -> killed, skin stolen, breaches as Form1_Disguised
        Pawn weakWorker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
            canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
        GenSpawn.Spawn(weakWorker, FindSafePawnCell(map, ntCell), map);
        CompPawnStatus weakStatus = weakWorker.GetComp<CompPawnStatus>()
            ?? throw new InvalidOperationException("Low-Fortitude worker missing CompPawnStatus.");
        weakStatus.SetPawnStatusEnabled(true, null);
        weakStatus.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 10;

        ntPlat.CurWorkType = EAnomalyWorkType.Attachment;
        ntPlat.Notify_StudyStart(weakWorker);
        abnNt.Notify_Studied(weakWorker, false);

        if (!weakWorker.Dead)
            throw new InvalidOperationException("Wiki Rule V/VI failed: Low-Fortitude (< IV) worker was not killed when finishing Contained Disguised NothingThere work.");
        if (!abnNt.Escaped || !heldNt.Spawned || ntPlat.HeldPawn != null)
            throw new InvalidOperationException("NothingThere did not breach containment after low-Fortitude panic/skin-steal.");
        if (Convert.ToInt32(propForm.GetValue(abnNt)) != 1 || !(bool)propHasDisguise.GetValue(abnNt))
            throw new InvalidOperationException("NothingThere did not enter Form1_Disguised with stolen skin after breach.");

        HediffDef form1Buff = DefDatabase<HediffDef>.GetNamed("LC_NothingThere_Form1Buff", true);
        if (!heldNt.health.hediffSet.HasHediff(form1Buff))
            throw new InvalidOperationException("Form1_Disguised missing LC_NothingThere_Form1Buff speed boost.");

        heldNt.Drawer.renderer.EnsureGraphicsInitialized();
        if (propDisguiseMat.GetValue(abnNt) == null)
            throw new InvalidOperationException("CompNothingThere.CachedDisguiseMaterial was null in Form1_Disguised.");
        PawnRenderNodeTagDef seamTag = DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail("NothingThere_DisguiseSeam");
        if (seamTag != null && heldNt.Drawer.renderer.renderTree.TryGetNodeByTag(seamTag, out PawnRenderNode seamNode))
            throw new InvalidOperationException($"NothingThere DisguiseSeam node unexpectedly exists in render tree: {seamNode}");
        heldNt.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, heldNt.DrawPos);
        heldNt.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, heldNt.DrawPos);
        heldNt.DynamicDrawPhaseAt(DrawPhase.Draw, heldNt.DrawPos);
        if (weakWorker.Corpse != null && weakWorker.Corpse.Spawned)
        {
            weakWorker.Corpse.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, weakWorker.Corpse.DrawPos);
            weakWorker.Corpse.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, weakWorker.Corpse.DrawPos);
            weakWorker.Corpse.DynamicDrawPhaseAt(DrawPhase.Draw, weakWorker.Corpse.DrawPos);
        }
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);

        DamageDef redDef = DefDatabase<DamageDef>.GetNamed("LC_RedDamage", true);
        DamageDef whiteDef = DefDatabase<DamageDef>.GetNamed("LC_WhiteDamage", true);
        DamageDef paleDef = DefDatabase<DamageDef>.GetNamed("LC_PaleDamage", true);

        // Verify four-color banner classification + ThingDef + dynamic resistance interface
        if (DamageUtils.ClassifyResistanceTier(0.0f) != FourColorResistanceTier.Immune
            || DamageUtils.ClassifyResistanceTier(0.3f) != FourColorResistanceTier.Resist
            || DamageUtils.ClassifyResistanceTier(0.6f) != FourColorResistanceTier.Endure
            || DamageUtils.ClassifyResistanceTier(1.0f) != FourColorResistanceTier.Normal
            || DamageUtils.ClassifyResistanceTier(1.4f) != FourColorResistanceTier.Weak
            || DamageUtils.ClassifyResistanceTier(2.0f) != FourColorResistanceTier.Fatal)
        {
            throw new InvalidOperationException("FourColorResistanceTier classification mismatch.");
        }
        ThingDef fourColorMoteDef = DefDatabase<ThingDef>.GetNamed("LC_Mote_FourColorDamage", true);
        ILCFourColorDynamicResistance ntDynResist = heldNt as ILCFourColorDynamicResistance
            ?? throw new InvalidOperationException("LC_NothingTherePawn must implement ILCFourColorDynamicResistance.");
        if (!ntDynResist.TryGetDynamicFourColorResistance(FourColorDamageType.Red, out float f1Red) || Math.Abs(f1Red - 0.3f) > 0.001f)
            throw new InvalidOperationException($"Form1 Red dynamic resistance expected 0.3, got {f1Red}.");

        // Form 2: Cocoon
        mToForm2.Invoke(abnNt, null);
        if (Convert.ToInt32(propForm.GetValue(abnNt)) != 2)
            throw new InvalidOperationException("NothingThere failed TransitionToForm2.");
        if (!ntDynResist.TryGetDynamicFourColorResistance(FourColorDamageType.White, out float f2White) || Math.Abs(f2White - 0.6f) > 0.001f)
            throw new InvalidOperationException($"Form2 White dynamic resistance expected 0.6, got {f2White}.");
        if (heldNt.Drawer.renderer.CurAnimation?.defName != "NothingThere_CocoonIdle")
            throw new InvalidOperationException($"Form2_Cocoon expected NothingThere_CocoonIdle animation, got {heldNt.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
        heldNt.Rotation = Rot4.East;
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);
        heldNt.Rotation = Rot4.West;
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);

        int inj2Before = heldNt.health.hediffSet.hediffs.Count(h => h is Hediff_Injury);
        int motes2Before = map.listerThings.ThingsOfDef(fourColorMoteDef).Count;
        heldNt.TakeDamage(new DamageInfo(redDef, 50f, 1f));
        if (heldNt.health.hediffSet.hediffs.Count(h => h is Hediff_Injury) != inj2Before)
            throw new InvalidOperationException("Form2_Cocoon failed Red 0.0 damage immunity.");
        Mote_FourColorDamage immuneBanner = map.listerThings.ThingsOfDef(fourColorMoteDef)
            .OfType<Mote_FourColorDamage>()
            .Skip(motes2Before)
            .LastOrDefault()
            ?? throw new InvalidOperationException("Immune Red hit on Form2_Cocoon did not spawn LC_Mote_FourColorDamage.");
        if (immuneBanner.damageType != FourColorDamageType.Red
            || immuneBanner.resistanceTier != FourColorResistanceTier.Immune
            || Math.Abs(immuneBanner.damageAmount) > 0.001f)
        {
            throw new InvalidOperationException($"Unexpected Form2 Immune banner payload: type={immuneBanner.damageType}, tier={immuneBanner.resistanceTier}, amt={immuneBanner.damageAmount}.");
        }

        DamageUtils.SpawnFourColorHitIndicator(FourColorDamageType.Pale, heldNt.DrawPos, map, 42f, 1.4f);
        Mote_FourColorDamage weakBanner = map.listerThings.ThingsOfDef(fourColorMoteDef)
            .OfType<Mote_FourColorDamage>()
            .LastOrDefault()
            ?? throw new InvalidOperationException("Weak Pale indicator did not spawn LC_Mote_FourColorDamage.");
        if (weakBanner.damageType != FourColorDamageType.Pale
            || weakBanner.resistanceTier != FourColorResistanceTier.Weak
            || Math.Abs(weakBanner.damageAmount - 42f) > 0.01f)
        {
            throw new InvalidOperationException($"Unexpected Weak banner payload: type={weakBanner.damageType}, tier={weakBanner.resistanceTier}, amt={weakBanner.damageAmount}.");
        }
        foreach (Thing m in map.listerThings.ThingsOfDef(fourColorMoteDef).ToList())
        {
            if (!m.Destroyed && m.Spawned) m.DeSpawn();
            if (!m.Destroyed) m.Destroy();
        }

        float hp2Before = heldNt.health.summaryHealth.SummaryHealthPercent;
        DamageWorker.DamageResult whiteRes = heldNt.TakeDamage(new DamageInfo(whiteDef, 30f, 1f, hitPart: heldNt.RaceProps.body.corePart));
        float hp2After = heldNt.health.summaryHealth.SummaryHealthPercent;
        if (hp2After >= hp2Before || whiteRes.totalDamageDealt <= 0f)
        {
            string hSummary = string.Join(", ", heldNt.health.hediffSet.hediffs.Select(h => $"{h.def.defName}@{h.Part?.Label ?? "null"}:{h.Severity:0.##}/imp={h.SummaryHealthPercentImpact:0.####}"));
            throw new InvalidOperationException(
                $"Form2_Cocoon did not take White damage. hpBefore={hp2Before:0.####}, hpAfter={hp2After:0.####}, " +
                $"dealt={whiteRes.totalDamageDealt}, deflected={whiteRes.deflected}, diminished={whiteRes.diminished}, " +
                $"hitPart={whiteRes.LastHitPart?.Label ?? "null"}, hediffs=[{hSummary}]");
        }

        // Form 3: True Form + Out-of-Combat Regen + Doubled Throw (44+76=120t) & Goodbye (120+48=168t) + Left/Right Facing
        mToForm3.Invoke(abnNt, null);
        if (Convert.ToInt32(propForm.GetValue(abnNt)) != 3)
            throw new InvalidOperationException("NothingThere failed TransitionToForm3.");
        HediffDef trueFormBuff = DefDatabase<HediffDef>.GetNamed("LC_NothingThere_TrueFormBuff", true);
        if (!heldNt.health.hediffSet.HasHediff(trueFormBuff))
            throw new InvalidOperationException("Form3_TrueForm missing LC_NothingThere_TrueFormBuff.");
        if (heldNt.Drawer.renderer.CurAnimation?.defName != "NothingThere_TrueIdle")
            throw new InvalidOperationException($"Form3_TrueForm expected NothingThere_TrueIdle animation, got {heldNt.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
        heldNt.Rotation = Rot4.East;
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);
        heldNt.Rotation = Rot4.West;
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);

        int inj3Before = heldNt.health.hediffSet.hediffs.Count(h => h is Hediff_Injury);
        heldNt.TakeDamage(new DamageInfo(redDef, 50f, 1f));
        if (heldNt.health.hediffSet.hediffs.Count(h => h is Hediff_Injury) != inj3Before)
            throw new InvalidOperationException("Form3_TrueForm failed Red 0.0 damage immunity.");

        float hp3Before = heldNt.health.summaryHealth.SummaryHealthPercent;
        heldNt.TakeDamage(new DamageInfo(paleDef, 15f, 1f));
        float hp3Damaged = heldNt.health.summaryHealth.SummaryHealthPercent;
        if (hp3Damaged >= hp3Before)
            throw new InvalidOperationException("Form3_TrueForm did not take Pale damage.");

        // Form 3 Out-of-combat Regen check: stays unhealed while lastDamagedTick is recent; heals after 600 ticks
        for (int i = 0; i < 60; i++) mTickBreachCombat.Invoke(abnNt, null);
        if (heldNt.health.summaryHealth.SummaryHealthPercent > hp3Damaged + 0.0001f)
            throw new InvalidOperationException("Form3_TrueForm regenerated prematurely before 600 ticks out of combat.");
        propLastDamaged.SetValue(abnNt, (Find.TickManager?.TicksGame ?? 0) - 700);
        for (int i = 0; i < 65; i++) mTickBreachCombat.Invoke(abnNt, null);
        if (heldNt.health.summaryHealth.SummaryHealthPercent <= hp3Damaged)
            throw new InvalidOperationException("Form3_TrueForm failed to regenerate once 10s out of combat.");

        Pawn throwTarget = PawnGenerator.GeneratePawn(animalKind, null);
        GenSpawn.Spawn(throwTarget, FindSafePawnCell(map, heldNt.Position), map);
        ThingDef boneProjDef = DefDatabase<ThingDef>.GetNamed("NothingThere_BoneSpikeProj", true);
        int projBefore = map.listerThings.ThingsOfDef(boneProjDef).Count;
        mStartThrow.Invoke(abnNt, new object[] { throwTarget });
        if (heldNt.Drawer.renderer.CurAnimation?.defName != "NothingThere_TrueThrow")
            throw new InvalidOperationException($"StartBoneSpikeThrow expected NothingThere_TrueThrow animation, got {heldNt.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);
        for (int i = 0; i < 43; i++) mTickBreachCombat.Invoke(abnNt, null);
        if (map.listerThings.ThingsOfDef(boneProjDef).Count > projBefore)
            throw new InvalidOperationException("StartBoneSpikeThrow launched projectile early before 44-tick windup finished.");
        mTickBreachCombat.Invoke(abnNt, null);
        if (map.listerThings.ThingsOfDef(boneProjDef).Count <= projBefore)
            throw new InvalidOperationException("StartBoneSpikeThrow did not launch NothingThere_BoneSpikeProj after 44-tick windup.");
        if (!(bool)propIsThrowingBoneSpike.GetValue(abnNt))
            throw new InvalidOperationException("Form3_TrueForm dropped IsThrowingBoneSpike state immediately instead of staying planted for 76-tick recovery.");
        for (int i = 0; i < 76; i++) mTickBreachCombat.Invoke(abnNt, null);
        if ((bool)propIsThrowingBoneSpike.GetValue(abnNt))
            throw new InvalidOperationException("IsThrowingBoneSpike did not clear after 76-tick recovery.");
        foreach (Thing p in map.listerThings.ThingsOfDef(boneProjDef).ToList())
        {
            if (!p.Destroyed && p.Spawned) p.DeSpawn();
            if (!p.Destroyed) p.Destroy();
        }
        if (!throwTarget.Destroyed && throwTarget.Spawned) { throwTarget.DeSpawn(); throwTarget.Destroy(); }

        Pawn slashTarget = PawnGenerator.GeneratePawn(animalKind, null);
        GenSpawn.Spawn(slashTarget, heldNt.Position, map);
        mStartGoodbye.Invoke(abnNt, new object[] { slashTarget });
        if (Convert.ToInt32(propGoodbyeWindup.GetValue(abnNt)) != 120)
            throw new InvalidOperationException($"StartGoodbyeWindup expected 120-tick windup, got {propGoodbyeWindup.GetValue(abnNt)}.");
        if (heldNt.Drawer.renderer.CurAnimation?.defName != "NothingThere_TrueGoodbye")
            throw new InvalidOperationException($"StartGoodbyeWindup expected NothingThere_TrueGoodbye animation, got {heldNt.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
        heldNt.Rotation = Rot4.East;
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);
        heldNt.Rotation = Rot4.West;
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);
        for (int i = 0; i < 120; i++) mTickBreachCombat.Invoke(abnNt, null);
        if (Convert.ToInt32(propGoodbyeSlash.GetValue(abnNt)) != 48 || !(bool)propIsPerformingGoodbye.GetValue(abnNt))
            throw new InvalidOperationException($"Goodbye slash expected 48-tick planted follow-through, got slash={propGoodbyeSlash.GetValue(abnNt)}.");
        heldNt.Drawer.renderer.RenderPawnAt(heldNt.DrawPos);
        for (int i = 0; i < 48; i++) mTickBreachCombat.Invoke(abnNt, null);
        if ((bool)propIsPerformingGoodbye.GetValue(abnNt))
            throw new InvalidOperationException("IsPerformingGoodbye did not clear after 48-tick follow-through.");
        if (!slashTarget.Destroyed && slashTarget.Spawned) { slashTarget.DeSpawn(); slashTarget.Destroy(); }

        // Pursuit resilience check: multi-part melee tools + BladeArm loss does NOT freeze pursuit after skill recovery
        if (ntDef.tools == null || ntDef.tools.Count < 4)
            throw new InvalidOperationException($"NothingThere expected >= 4 backup melee tools, found {ntDef.tools?.Count ?? 0}.");
        BodyPartRecord bladePart = heldNt.health.hediffSet.GetNotMissingParts()
            .FirstOrDefault(p => p.def.defName == "NothingThere_BladeArm")
            ?? throw new InvalidOperationException("NothingThere missing NothingThere_BladeArm part record.");
        heldNt.health.AddHediff(RimWorld.HediffDefOf.MissingBodyPart, bladePart);

        IntVec3 chaseCell = IntVec3.Invalid;
        foreach (IntVec3 offset in new[]
        {
            new IntVec3(9, 0, 0), new IntVec3(-9, 0, 0),
            new IntVec3(0, 0, 9), new IntVec3(0, 0, -9),
            new IntVec3(7, 0, 7), new IntVec3(-7, 0, -7)
        })
        {
            IntVec3 c = heldNt.Position + offset;
            if (c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetRegion(map) != null
                && c.GetEdifice(map) == null && heldNt.CanReach(c, PathEndMode.Touch, Danger.Deadly, canBashDoors: true))
            {
                chaseCell = c;
                break;
            }
        }
        if (!chaseCell.IsValid)
        {
            chaseCell = FindSafePawnCell(map, heldNt.Position);
        }

        Pawn chaseTarget = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
            canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f,
            allowDowned: false, validatorPreGear: p => p.story?.traits == null || !p.story.traits.allTraits.Any(t => t.def.defName == "Wimp")));
        GenSpawn.Spawn(chaseTarget, chaseCell, map);
        chaseTarget.equipment?.DestroyAllEquipment();
        if (chaseTarget.drafter != null) chaseTarget.drafter.Drafted = true;
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        if (heldNt.TryGetAttackVerb(chaseTarget, true, true) == null)
            throw new InvalidOperationException("NothingThere TryGetAttackVerb returned null when BladeArm was missing.");

        // Spawn perpendicular projectile decoy so BoneSpike fires safely without killing chaseTarget, then verify full skill lock -> resume pursuit
        Pawn spikeDecoy = PawnGenerator.GeneratePawn(animalKind, null);
        IntVec3 decoyCell = FindSafePawnCell(map, heldNt.Position);
        if (decoyCell == chaseTarget.Position) decoyCell = heldNt.Position;
        GenSpawn.Spawn(spikeDecoy, decoyCell, map);

        IntVec3 ntStartPos = heldNt.Position;
        mStartThrow.Invoke(abnNt, new object[] { spikeDecoy });
        for (int i = 0; i < 150 && (bool)propIsThrowingBoneSpike.GetValue(abnNt); i++)
        {
            Find.TickManager.DoSingleTick();
            if (heldNt.Position != ntStartPos)
                throw new InvalidOperationException($"NothingThere moved during BoneSpike skill lock ({ntStartPos} -> {heldNt.Position}).");
        }
        if ((bool)propIsThrowingBoneSpike.GetValue(abnNt))
            throw new InvalidOperationException("NothingThere did not finish BoneSpike skill lock within 150 ticks.");
        if (!spikeDecoy.Destroyed && spikeDecoy.Spawned) { spikeDecoy.DeSpawn(); spikeDecoy.Destroy(); }
        foreach (Thing p in map.listerThings.ThingsOfDef(boneProjDef).ToList())
        {
            if (!p.Destroyed && p.Spawned) p.DeSpawn();
            if (!p.Destroyed) p.Destroy();
        }

        // Wait up to 60 real ticks for chase AI to engage & advance toward target (with BladeArm still missing)
        for (int i = 0; i < 60; i++)
        {
            Find.TickManager.DoSingleTick();
            if (heldNt.CurJobDef == RimWorld.JobDefOf.AttackMelee
                && heldNt.CurJob.canBashDoors
                && (heldNt.Position != ntStartPos
                    || (heldNt.CurJob.targetA.Thing != null && heldNt.Position.AdjacentTo8WayOrInside(heldNt.CurJob.targetA.Thing))))
            {
                break;
            }
        }
        if (heldNt.CurJobDef != RimWorld.JobDefOf.AttackMelee || !heldNt.CurJob.canBashDoors)
            throw new InvalidOperationException($"NothingThere failed to resume AttackMelee pursuit with missing BladeArm (job={heldNt.CurJobDef?.defName ?? "<null>"}, canBashDoors={heldNt.CurJob?.canBashDoors}).");
        bool adjacentToCurTarget = heldNt.CurJob.targetA.Thing != null && heldNt.Position.AdjacentTo8WayOrInside(heldNt.CurJob.targetA.Thing);
        if (heldNt.Position == ntStartPos && heldNt.pather?.Moving != true && !adjacentToCurTarget)
            throw new InvalidOperationException($"NothingThere stalled during pursuit after skill recovery (pos={heldNt.Position}, moving={heldNt.pather?.Moving}, target={heldNt.CurJob.targetA.Thing?.LabelShort ?? "<null>"}).");

        // Morph transition must restore missing limbs
        mToForm3.Invoke(abnNt, null);
        if (heldNt.health.hediffSet.PartIsMissing(bladePart))
            throw new InvalidOperationException("TransitionToForm3 did not restore missing NothingThere_BladeArm.");

        if (!chaseTarget.Destroyed && chaseTarget.Spawned) { chaseTarget.DeSpawn(); chaseTarget.Destroy(); }
        foreach (Thing m in map.listerThings.ThingsOfDef(fourColorMoteDef).ToList())
        {
            if (!m.Destroyed && m.Spawned) m.DeSpawn();
            if (!m.Destroyed) m.Destroy();
        }

        // Suppression death effect -> NothingThereEgg with matching biosignature
        var dyingNt = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("DyingNothingThere", true), heldNt.Position, map);
        dyingNt.InitWith(heldNt);
        heldNt.DeSpawn();
        Find.WorldPawns.PassToWorld(heldNt, PawnDiscardDecideMode.Decide);
        dyingNt.Complete();

        Thing returnedNtEgg = map.listerThings.ThingsOfDef(ntEggDef)
            .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == ntSig);
        if (returnedNtEgg == null)
            throw new InvalidOperationException("NothingThere biosignature did not survive suppression and egg return.");
        CleanupTestContainmentTerminal(ntPlat);

        // 6. Leave one contained NothingThere room + healthy Mimicry veteran for Save/Reload validation
        const int ntSavedSig = 0x4c430091;
        var ntSavedPlat = SpawnTestContainmentTerminal(map);
        Thing ntSavedEgg = ThingMaker.MakeThing(ntEggDef);
        AbnormalityUtility.TransferSignature(ntSavedEgg, ntSavedSig);
        if (!ntSavedPlat.TryCaptureThing(ntSavedEgg) || ntSavedPlat.HeldPawn?.def?.defName != "NothingThere")
            throw new InvalidOperationException("Saved NothingThere egg failed to enter room containment.");

        var leftoverInjuries = new List<Hediff_Injury>();
        veteran.health.hediffSet.GetHediffs(ref leftoverInjuries);
        foreach (Hediff_Injury inj in leftoverInjuries)
            inj.Heal(1000f);

        Log.Message("[LC17 Validation] GAMEPLAY PASS; NothingThere; aleph/work/ego/lifesteal/3forms/red_immune/egg_return");

        ValidatePhase1And2(map, abnSettings);
        ValidatePhase3(map, abnSettings);
        ValidatePhase4(map, abnSettings);
        ValidateRoomContainment(map);
        ValidateAbnormalityExtraction(map);
    }

    public static void CheckReload(Map map)
    {
        int index = 0;
        foreach (string name in new[] { "OneSin", "Laetitia", "MeatLantern", "BurrowingHeaven" })
        {
            int signature = 0x4c430010 + index++;
            if (!map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed(name + "Egg"))
                .Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == signature))
                throw new InvalidOperationException("Egg identity did not survive save/load: " + name);
            var entry = DefDatabase<AbnormalityCodexEntryDef>.GetNamedSilentFail(name);
            if (entry != null && !entry.Discovered) throw new InvalidOperationException("Codex did not survive save/load: " + name);
        }

        ThingDef portraitEggDef = DefDatabase<ThingDef>.GetNamed("PortraitOfAnotherWorldEgg", true);
        if (!map.listerThings.ThingsOfDef(portraitEggDef).Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == 0x4c430081))
            throw new InvalidOperationException("Ejected PortraitOfAnotherWorldEgg identity did not survive save/load.");

        bool foundSavedBoundPortrait = false;
        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "PortraitOfAnotherWorld" && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430082)
            {
                CompAbnormalityTool comp = plat.HeldToolComp;
                PropertyInfo isBoundProp = comp?.GetType().GetProperty("IsBound");
                if (comp != null && isBoundProp != null && (bool)isBoundProp.GetValue(comp))
                {
                    plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
                    plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
                    plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);
                    plat.HeldPawn?.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.HeldPawn.DrawPos);
                    plat.HeldPawn?.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.HeldPawn.DrawPos);
                    plat.HeldPawn?.DynamicDrawPhaseAt(DrawPhase.Draw, plat.HeldPawn.DrawPos);
                    foundSavedBoundPortrait = true;
                    break;
                }
            }
        }
        if (!foundSavedBoundPortrait)
            throw new InvalidOperationException("Bound PortraitOfAnotherWorld on ContainmentTerminal did not survive save/load.");

        bool foundSavedEnclosedIron = false;
        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "WeCanChangeAnything" && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430086)
            {
                CompAbnormalityTool comp = plat.HeldToolComp;
                if (comp != null && comp.HasEnclosedOccupant && comp.EnclosedOccupant != null && !comp.EnclosedOccupant.Spawned && comp.IsActiveContinuous)
                {
                    foundSavedEnclosedIron = true;
                    break;
                }
            }
        }
        if (!foundSavedEnclosedIron)
            throw new InvalidOperationException("Enclosed occupant inside WeCanChangeAnything on ContainmentTerminal did not survive save/load.");

        bool foundSavedTrophyPlatform = false;
        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "BurrowingHeaven" && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430089)
            {
                Pawn heldBh = plat.HeldPawn;
                Assembly lcAbnAsm = heldBh.GetType().Assembly;
                Type recordType = lcAbnAsm.GetType("BurrowingHeaven.Things.HangingHeadRecord", throwOnError: true);
                Type compType = lcAbnAsm.GetType("BurrowingHeaven.Comp.CompBurrowingHeaven", throwOnError: true);
                ThingComp bhComp = heldBh.AllComps.FirstOrDefault(c => compType.IsInstanceOfType(c));
                MethodInfo getHead = compType.GetMethod("GetHangingHead", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo capturedLayersField = recordType.GetField("capturedLayers", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo hasUsableProp = recordType.GetProperty("HasUsableCapturedLayers", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getCompositeMethod = recordType.GetMethod("GetCompositeLayers", BindingFlags.Public | BindingFlags.Instance);

                object vanillaRec = getHead.Invoke(bhComp, new object[] { 0 })
                    ?? throw new InvalidOperationException("Saved BurrowingHeaven slot 0 HangingHeadRecord missing after reload.");
                IList vanillaCaptured = (IList)capturedLayersField.GetValue(vanillaRec);
                if (vanillaCaptured == null || vanillaCaptured.Count < 2 || !(bool)hasUsableProp.GetValue(vanillaRec))
                    throw new InvalidOperationException("Saved BurrowingHeaven slot 0 capturedLayers invalid after reload.");
                if (((IList)getCompositeMethod.Invoke(vanillaRec, null)).Count < 3)
                    throw new InvalidOperationException("Saved BurrowingHeaven slot 0 composite layers invalid after reload.");

                if (DefDatabase<PawnKindDef>.GetNamedSilentFail("Monolyn_WandererPlayer") != null)
                {
                    object monolynRec = getHead.Invoke(bhComp, new object[] { 1 })
                        ?? throw new InvalidOperationException("Saved BurrowingHeaven slot 1 Monolyn HangingHeadRecord missing after reload.");
                    IList monoCaptured = (IList)capturedLayersField.GetValue(monolynRec);
                    bool hasHairF = false;
                    bool hasHairB = false;
                    if (monoCaptured != null)
                    {
                        foreach (object layerObj in monoCaptured)
                        {
                            string texPath = (string)layerObj.GetType().GetField("texPath").GetValue(layerObj);
                            if (texPath != null && texPath.IndexOf("HairF", StringComparison.OrdinalIgnoreCase) >= 0) hasHairF = true;
                            if (texPath != null && texPath.IndexOf("HairB", StringComparison.OrdinalIgnoreCase) >= 0) hasHairB = true;
                        }
                    }
                    if (!hasHairF || !hasHairB || !(bool)hasUsableProp.GetValue(monolynRec))
                        throw new InvalidOperationException("Saved Monolyn HairF/HairB capturedLayers did not survive save/load.");
                }

                plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);
                foundSavedTrophyPlatform = true;
                break;
            }
        }
        if (!foundSavedTrophyPlatform)
            throw new InvalidOperationException("Saved BurrowingHeaven trophy platform did not survive save/load.");

        bool foundSavedNtPlatform = false;
        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "NothingThere" && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430091)
            {
                plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);
                plat.HeldPawn?.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.HeldPawn.DrawPos);
                plat.HeldPawn?.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.HeldPawn.DrawPos);
                plat.HeldPawn?.DynamicDrawPhaseAt(DrawPhase.Draw, plat.HeldPawn.DrawPos);
                foundSavedNtPlatform = true;
                break;
            }
        }
        if (!foundSavedNtPlatform)
            throw new InvalidOperationException("Saved NothingThere containment terminal did not survive save/load.");

        HediffDef regenDef = DefDatabase<HediffDef>.GetNamed("LC_MimicrySuitRegen", true);
        if (!map.mapPawns.FreeColonistsSpawned.Any(p =>
            p.equipment?.Primary?.def?.defName == "EgoWeapon_NothingThere"
            && p.apparel?.WornApparel?.Any(a => a.def.defName == "Apparel_NothingThereSuit") == true
            && p.health?.hediffSet?.HasHediff(regenDef) == true))
        {
            throw new InvalidOperationException("Mimicry E.G.O weapon/suit or LC_MimicrySuitRegen did not survive save/load.");
        }

        GameComponent_LC gc = GameComponent_LC.Instance;
        if (gc == null || !map.mapPawns.FreeColonistsSpawned.Any(p => p.equipment?.Primary != null && gc.IsWeaponBlessed(p.equipment.Primary)))
        {
            string colDiag = string.Join(", ", map.mapPawns.FreeColonistsSpawned.Select(p =>
                $"{p.ThingID}@{p.Position}:down={p.Downed}:wep={p.equipment?.Primary?.ThingID ?? "null"}"));
            throw new InvalidOperationException($"Blessed weapon state in GameComponent_LC did not survive save/load. gcNull={gc == null}, colonists=[{colDiag}]");
        }

        Pawn reloadedMimicryPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p =>
            p.equipment?.Primary?.def?.defName == "EgoWeapon_NothingThere");
        if (reloadedMimicryPawn == null || !gc.IsEgoWeaponAutoCastEnabled(reloadedMimicryPawn.equipment.Primary))
        {
            throw new InvalidOperationException("E.G.O. weapon auto-cast toggle state in GameComponent_LC did not survive save/load.");
        }

        ValidatePhase1And2AfterLoad(map);
        ValidatePhase3AfterLoad(map);
        ValidatePhase4AfterLoad(map);
        ValidateRoomContainmentAfterLoad(map);
    }

    private static IntVec3 FindFarthestSafeCell(Map map, int minColonistDist = 0)
    {
        List<IntVec3> hostiles = map.mapPawns.AllPawnsSpawned
            .Where(p => p != null && !p.Dead && p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer))
            .Select(p => p.Position)
            .ToList();
        List<IntVec3> colonists = minColonistDist > 0
            ? map.mapPawns.FreeColonistsSpawned.Select(p => p.Position).ToList()
            : null;
        IntVec3 best = IntVec3.Invalid;
        int bestDistSq = -1;
        for (int x = 12; x < map.Size.x - 12; x += 4)
        {
            for (int z = 12; z < map.Size.z - 12; z += 4)
            {
                IntVec3 c = new IntVec3(x, 0, z);
                if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetRegion(map) == null
                    || c.GetFirstBuilding(map) != null || c.GetEdifice(map) != null || c.GetFirstPawn(map) != null)
                {
                    continue;
                }
                if (colonists != null && colonists.Any(col => (c.x - col.x) * (c.x - col.x) + (c.z - col.z) * (c.z - col.z) < minColonistDist * minColonistDist))
                {
                    continue;
                }
                int minDistSq = hostiles.Count == 0
                    ? 0
                    : hostiles.Min(h => (c.x - h.x) * (c.x - h.x) + (c.z - h.z) * (c.z - h.z));
                if (minDistSq > bestDistSq)
                {
                    bestDistSq = minDistSq;
                    best = c;
                }
            }
        }
        return best.IsValid ? best : FindSafePawnCell(map, map.Center);
    }

    private static Pawn SpawnValidationColonist(Map map, IntVec3 near)
    {
        Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f,
            allowDowned: false,
            allowAddictions: false,
            validatorPreGear: c => c.story?.traits == null || !c.story.traits.allTraits.Any(t => t.def.defName == "Wimp")));
        GenSpawn.Spawn(p, FindSafePawnCell(map, near), map);
        return p;
    }

    private static void ValidatePhase1And2(Map map, object abnSettings)
    {
        (string name, string cat, bool isTool)[] all10 =
        {
            ("FairyFestival", "ZAYIN", false),
            ("WellCheers", "ZAYIN", false),
            ("PunishingBird", "TETH", false),
            ("VoidDream", "TETH", false),
            ("HeartOfAspiration", "TETH", true),
            ("ScorchedGirl", "TETH", false),
            ("DerFreischutz", "HE", false),
            ("FuneralOfDeadButterflies", "HE", false),
            ("SnowQueen", "HE", false),
            ("NotesFromCrazedResearcher", "HE", true),
        };

        // 1. Codex, ritual extraction pool, E.G.O gear defs, and settings eligibility toggles
        foreach (var (name, cat, isTool) in all10)
        {
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamed(name, true);
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamed(name + "Egg", true);
            if (AbnormalityUtility.ResolveCodexEntry(raceDef)?.defName != name
                || AbnormalityUtility.ResolveCodexEntry(eggDef)?.defName != name)
            {
                throw new InvalidOperationException($"Codex entry resolution failed for {name} / {name}Egg.");
            }

            if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual(cat).Contains(eggDef))
            {
                throw new InvalidOperationException($"{name}Egg missing from {cat} ritual extraction list.");
            }

            if (!isTool)
            {
                DefDatabase<ThingDef>.GetNamed("EgoWeapon_" + name, true);
                DefDatabase<ThingDef>.GetNamed("Apparel_" + name + "Suit", true);
                DefDatabase<ThingDef>.GetNamed("Accessory_" + name, true);
            }

            FieldInfo toggleField = abnSettings.GetType().GetField("Enable" + name)
                ?? throw new InvalidOperationException($"Missing setting toggle Enable{name}.");
            bool prev = (bool)toggleField.GetValue(abnSettings);
            try
            {
                toggleField.SetValue(abnSettings, false);
                if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual(cat).Contains(eggDef))
                {
                    throw new InvalidOperationException($"Enable{name}=false did not filter {name}Egg.");
                }
            }
            finally
            {
                toggleField.SetValue(abnSettings, prev);
            }
        }

        // 2. Standard containment, work, PeBox, E.G.O extraction, rendering, and egg-return across all 8 standard entities
        int pIndex = 0;
        Pawn worker = null;
        foreach (var (name, _, isTool) in all10)
        {
            if (isTool) continue;

            worker = SpawnValidationColonist(map, map.Center);
            worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
            CompPawnStatus emp = worker.GetComp<CompPawnStatus>() ?? throw new InvalidOperationException("Phase1And2 validation worker missing CompPawnStatus.");

            var plat = SpawnTestContainmentTerminal(map);
            IntVec3 cell = plat.Position;

            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg", true));
            int sig = 0x4c430100 + pIndex++;
            AbnormalityUtility.TransferSignature(egg, sig);
            if (!plat.TryCaptureThing(egg)) throw new InvalidOperationException($"Egg failed to enter room containment: {name}");

            Pawn held = plat.HeldPawn ?? throw new InvalidOperationException($"Egg failed to enter containment: {name}");
            CompAbnormality abn = held.GetComp<CompAbnormality>() ?? throw new InvalidOperationException($"Missing CompAbnormality: {name}");
            if (abn.biosignature != sig) throw new InvalidOperationException($"Identity mismatch: {name}");

            AbnormalityUtility.Discover(held.def);
            plat.GetGizmos().ToList();
            held.Drawer.renderer.EnsureGraphicsInitialized();
            plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
            plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
            plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);

            plat.CurWorkType = Enum.GetValues(typeof(EAnomalyWorkType)).Cast<EAnomalyWorkType>()
                .OrderByDescending(w => abn.CheckStudierSkillRequire(emp, w)).First();
            float rate = abn.CheckStudierSkillRequire(emp, plat.CurWorkType);
            if (rate <= 0f) throw new InvalidOperationException($"Worker cannot work on {name}: rate={rate}");

            abn.PeBoxComp.CurAmountIndiPebox = 0;
            Rand.PushState(sig);
            try
            {
                for (int i = 0; i < 24; i++) plat.Notify_StudyInterval(emp);
            }
            finally { Rand.PopState(); }
            abn.Notify_Studied(worker);
            if (abn.PeBoxComp.CurAmountIndiPebox <= 0) throw new InvalidOperationException($"No PeBox produced by {name}");

            held.GetComp<CompAbnormalityStudiable>().Study(worker, 1000f);
            CompEgoExtractable ego = held.GetComp<CompEgoExtractable>();
            if (ego != null && ego.Props.weaponExtracted != null)
            {
                abn.PeBoxComp.CurAmountIndiPebox = 1000;
                int before = ego.CurAmountWeapon;
                typeof(CompEgoExtractable).GetMethod("ExtractEGO", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ego, new object[] { EGO_TYPE.Weapon });
                if (ego.CurAmountWeapon != before + 1) throw new InvalidOperationException($"EGO extraction failed: {name}");
            }

            plat.EjectContents();
            var fx = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Dying" + name, true), cell, map);
            fx.InitWith(held);
            if (held.Spawned) held.DeSpawn();
            Find.WorldPawns.PassToWorld(held, PawnDiscardDecideMode.Discard);
            fx.Complete();
            Thing returnedEgg = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed(name + "Egg", true))
                .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == sig);
            if (returnedEgg == null) throw new InvalidOperationException($"Identity did not survive egg return: {name}");
            CleanupTestContainmentTerminal(plat);
        }

        // 3. Unique Mechanics Verification
        worker = SpawnValidationColonist(map, map.Center);
        // 3a. FairyFestival care + jealousy devour on studying another abnormality
        var fairyPlat = SpawnTestContainmentTerminal(map);
        Thing fEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("FairyFestivalEgg", true));
        if (!fairyPlat.TryCaptureThing(fEgg)) throw new InvalidOperationException("FairyFestivalEgg failed to enter room containment.");

        var otherPlat = SpawnTestContainmentTerminal(map);
        Thing wEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WellCheersEgg", true));
        if (!otherPlat.TryCaptureThing(wEgg)) throw new InvalidOperationException("WellCheersEgg failed to enter room containment.");

        Pawn betrayer = SpawnValidationColonist(map, fairyPlat.Position);
        fairyPlat.HeldPawn.GetComp<CompAbnormality>().Notify_Studied(betrayer);
        HediffDef fairyCareDef = DefDatabase<HediffDef>.GetNamed("LC_FairyCare", true);
        if (!betrayer.health.hediffSet.HasHediff(fairyCareDef))
            throw new InvalidOperationException("FairyFestival did not apply LC_FairyCare after study.");
        otherPlat.Notify_StudyStart(betrayer);
        if (!betrayer.Dead)
            throw new InvalidOperationException("LC_FairyCare failed to devour worker who started studying another Abnormality.");
        CleanupTestContainmentTerminal(fairyPlat);

        // 3a2. WellCheers multi-part animations, work-type flavor mapping, direct right-click FloatMenu & wave/bubble VFX
        Pawn wcHeld = otherPlat.HeldPawn;
        CompAbnormality wcAbn = wcHeld.GetComp<CompAbnormality>();
        Pawn wcDrinker = SpawnValidationColonist(map, otherPlat.Position);
        otherPlat.CurWorkType = EAnomalyWorkType.Instinct;
        otherPlat.Notify_StudyStart(wcDrinker);
        if (wcAbn.GetType().GetProperty("CurrentFlavor").GetValue(wcAbn).ToString() != "Red")
            throw new InvalidOperationException("WellCheers Instinct work did not switch flavor to Red.");
        otherPlat.CurWorkType = EAnomalyWorkType.Insight;
        otherPlat.Notify_StudyStart(wcDrinker);
        if (wcAbn.GetType().GetProperty("CurrentFlavor").GetValue(wcAbn).ToString() != "Blue")
            throw new InvalidOperationException("WellCheers Insight work did not switch flavor to Blue.");
        if (wcHeld.Drawer.renderer.CurAnimation?.defName != "WellCheers_Work")
            throw new InvalidOperationException($"WellCheers expected WellCheers_Work animation during work, got {wcHeld.Drawer.renderer.CurAnimation?.defName}.");
        List<FloatMenuOption> wcOpts = otherPlat.GetFloatMenuOptions(wcDrinker).ToList();
        if (wcOpts.Count < 3 || wcOpts.Count(o => o.action != null) < 3)
            throw new InvalidOperationException($"WellCheers holding platform expected 3 enabled direct-drink right-click options, got {wcOpts.Count}.");
        foreach (string animName in new[] { "WellCheers_Idle", "WellCheers_Work", "WellCheers_Dispense", "WellCheers_Abduct" })
        {
            AnimationDef animDef = DefDatabase<AnimationDef>.GetNamed(animName, true);
            wcHeld.Drawer.renderer.SetAnimation(animDef);
            otherPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, otherPlat.DrawPos);
            otherPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, otherPlat.DrawPos);
            otherPlat.DynamicDrawPhaseAt(DrawPhase.Draw, otherPlat.DrawPos);
        }
        wcAbn.GetType().GetMethod("CompleteDirectDrink").Invoke(wcAbn, new object[] { wcDrinker, Enum.Parse(wcAbn.GetType().GetProperty("CurrentFlavor").PropertyType, "Red") });
        List<FloatMenuOption> wcCooldownOpts = otherPlat.GetFloatMenuOptions(wcDrinker).ToList();
        if (wcCooldownOpts.Count == 0 || wcCooldownOpts.All(o => o.action != null))
            throw new InvalidOperationException("WellCheers direct drink did not enter cooldown state on right-click menu.");
        foreach (string wcSound in new[]
        {
            "LC_WellCheers_Drink", "LC_WellCheers_Drink_Male", "LC_WellCheers_Drink_Female",
            "LC_WellCheers_Dispense", "LC_WellCheers_Sleep", "LC_WellCheers_Dance",
            "LC_WellCheers_WorkLoop", "LC_WellCheers_EgoShot"
        })
        {
            SoundDef sDef = DefDatabase<SoundDef>.GetNamed(wcSound, true);
            bool expectSustain = wcSound == "LC_WellCheers_Dance" || wcSound == "LC_WellCheers_WorkLoop";
            if (sDef.sustain != expectSustain)
                throw new InvalidOperationException($"WellCheers SoundDef {wcSound} sustain mismatch (expected {expectSustain}, got {sDef.sustain}).");
        }
        Thing seaFx = GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("LC_FX_WellCheersSeaWave", true), otherPlat.Position, map);
        Thing bubbleFx = GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("LC_FX_WellCheersBubble", true), otherPlat.Position, map);
        Find.TickManager.DoSingleTick();
        seaFx.Destroy();
        bubbleFx.Destroy();
        otherPlat.Destroy();

        // 3b. PunishingBird breach -> damage triggers Enraged Maw & 0.1 dynamic four-color resistance -> return to egg
        Pawn pb = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("PunishingBird", true), LCContainmentUtility.AbnormalityFaction);
        GenSpawn.Spawn(pb, FindSafePawnCell(map, map.Center), map);
        Find.TickManager.DoSingleTick();
        CompAbnormality pbAbn = pb.GetComp<CompAbnormality>();
        int pbSig = pbAbn.biosignature;
        PropertyInfo isEnragedProp = pbAbn.GetType().GetProperty("IsEnraged", BindingFlags.Public | BindingFlags.Instance);
        if ((bool)isEnragedProp.GetValue(pbAbn))
            throw new InvalidOperationException("PunishingBird should start calm upon breaching.");
        DamageDef redDef = DefDatabase<DamageDef>.GetNamed("LC_RedDamage", true);
        pb.TakeDamage(new DamageInfo(redDef, 2f, instigator: worker));
        if (!(bool)isEnragedProp.GetValue(pbAbn))
            throw new InvalidOperationException("PunishingBird failed to enter Enraged state when attacked.");
        if (!(pb is ILCFourColorDynamicResistance dynRes)
            || !dynRes.TryGetDynamicFourColorResistance(FourColorDamageType.Red, out float pbRes)
            || Math.Abs(pbRes - 0.1f) > 0.001f)
        {
            throw new InvalidOperationException("Enraged PunishingBird did not expose 0.1 dynamic resistance.");
        }
        pbAbn.GetType().GetMethod("ReturnQuietlyToEgg", BindingFlags.Public | BindingFlags.Instance).Invoke(pbAbn, null);
        if (!map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("PunishingBirdEgg", true))
            .Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == pbSig))
        {
            throw new InvalidOperationException("PunishingBird ReturnQuietlyToEgg failed to spawn matching egg.");
        }

        // 3c. VoidDream sleep hediff consciousness cap & Wake Sleeper gizmo
        Pawn sleeper = SpawnValidationColonist(map, map.Center);
        HediffDef sleepDef = DefDatabase<HediffDef>.GetNamed("LC_VoidDreamSleep", true);
        Hediff sleepHediff = HediffMaker.MakeHediff(sleepDef, sleeper);
        sleeper.health.AddHediff(sleepHediff);
        if (sleeper.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) > 0.15f)
            throw new InvalidOperationException("LC_VoidDreamSleep failed to cap consciousness.");
        string wakeLabel = "LC_VoidDream_WakeGizmoLabel".Translate();
        if (!sleeper.GetGizmos().Any(g => g is Command_Action ca && ca.defaultLabel == wakeLabel))
            throw new InvalidOperationException("LC_VoidDreamSleep missing Wake Sleeper gizmo.");
        sleeper.health.RemoveHediff(sleepHediff);

        // 3d. DerFreischutz request shot & PeBox deduction / counter increment
        var derPlat = SpawnTestContainmentTerminal(map);
        IntVec3 cornerCell = derPlat.Position;
        Thing dEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("DerFreischutzEgg", true));
        if (!derPlat.TryCaptureThing(dEgg)) throw new InvalidOperationException("DerFreischutzEgg failed to enter room containment.");
        CompAbnormality derAbn = derPlat.HeldPawn.GetComp<CompAbnormality>();
        derAbn.PeBoxComp.CurAmountIndiPebox = 20;
        IntVec3 safeAim = new IntVec3(cornerCell.x + 2 <= map.Size.x - 2 ? cornerCell.x + 2 : cornerCell.x - 2, 0, cornerCell.z);
        bool fired = (bool)derAbn.GetType().GetMethod("RequestMagicBulletAt", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(derAbn, new object[] { safeAim });
        int shotsFired = (int)derAbn.GetType().GetProperty("ShotsFired", BindingFlags.Public | BindingFlags.Instance).GetValue(derAbn);
        if (!fired || derAbn.PeBoxComp.CurAmountIndiPebox != 10 || shotsFired != 1)
            throw new InvalidOperationException($"DerFreischutz request failed: fired={fired}, pe={derAbn.PeBoxComp.CurAmountIndiPebox}, shots={shotsFired}");
        foreach (Thing p in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("MagicBullet_Request", true)).ToList())
        {
            if (!p.Destroyed) p.Destroy();
        }
        CleanupTestContainmentTerminal(otherPlat);
        CleanupTestContainmentTerminal(derPlat);

        // 3e. HeartOfAspiration equip -> combat -> safe return AND second active equipper saved across save/load
        var (heartPlat1, _, heartTool1) = ContainToolOnPlatform(map, worker, "HeartOfAspiration", 0x4c430201);
        Pawn heartTester = SpawnValidationColonist(map, map.Center);
        heartTool1.OnEquipped(heartTester);
        HediffDef heartBuffDef = DefDatabase<HediffDef>.GetNamed("LC_HeartOfAspirationBuff", true);
        if (!heartTool1.IsEquipped || heartTool1.ActiveEquipper != heartTester || !heartTester.health.hediffSet.HasHediff(heartBuffDef))
            throw new InvalidOperationException("HeartOfAspiration OnEquipped failed.");
        heartTool1.GetType().GetMethod("NotifyCombatDamageDealt", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(heartTool1, new object[] { heartTester, worker, 12f });
        heartTool1.OnUnequipped(heartTester, 900, returnedNormally: true);
        if (heartTool1.IsEquipped || heartTester.InMentalState)
            throw new InvalidOperationException("HeartOfAspiration safe return after combat damage failed.");
        CleanupTestContainmentTerminal(heartPlat1);

        Pawn savedHeartBearer = SpawnValidationColonist(map, FindFarthestSafeCell(map, minColonistDist: 10));
        if (savedHeartBearer.drafter != null) savedHeartBearer.drafter.Drafted = true;
        var (_, _, savedHeartTool) = ContainToolOnPlatform(map, savedHeartBearer, "HeartOfAspiration", 0x4c430202);
        savedHeartTool.OnEquipped(savedHeartBearer);
        savedHeartTool.GetType().GetMethod("NotifyCombatDamageDealt", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(savedHeartTool, new object[] { savedHeartBearer, worker, 10f });

        // 3f. SnowQueen render tree / animations / freeze captive / duel float menu & fortitude scaling
        foreach (string shaderName in new[] { "LC_SnowQueenFrostAura", "LC_SnowQueenIceCrystal", "LC_ResearcherNoteCore", "LC_ResearcherNoteAura" })
        {
            ShaderDef sDef = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderName);
            if (sDef == null || sDef.Shader == null)
                throw new InvalidOperationException($"ShaderDef '{shaderName}' failed to resolve or compile.");
        }

        foreach (string animName in new[]
        {
            "SnowQueen_Idle", "SnowQueen_Work", "SnowQueen_KissFirst", "SnowQueen_KissSecond",
            "SnowQueen_KissThird", "SnowQueen_Duel", "SnowQueen_DuelWin"
        })
        {
            if (DefDatabase<AnimationDef>.GetNamedSilentFail(animName) == null)
                throw new InvalidOperationException($"SnowQueen AnimationDef '{animName}' missing.");
        }

        Pawn savedCaptive = SpawnValidationColonist(map, map.Center);
        var sqPlat = SpawnTestContainmentTerminal(map);
        Thing sqEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SnowQueenEgg", true));
        AbnormalityUtility.TransferSignature(sqEgg, 0x4c430203);
        if (!sqPlat.TryCaptureThing(sqEgg)) throw new InvalidOperationException("SnowQueenEgg failed to enter room containment.");
        if (sqPlat.HeldPawn?.def?.race?.renderTree?.defName != "SnowQueen")
            throw new InvalidOperationException("SnowQueen race missing SnowQueen RenderTreeDef.");

        CompAbnormality sqAbn = sqPlat.HeldPawn.GetComp<CompAbnormality>();
        MethodInfo mGetWinChance = sqAbn.GetType().GetMethod("GetFortitudeWinChance", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("SnowQueen missing public static GetFortitudeWinChance.");
        Pawn fortTester = SpawnValidationColonist(map, map.Center);
        CompPawnStatus fortStatus = fortTester.GetComp<CompPawnStatus>();
        fortStatus.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 10;
        float chanceI = (float)mGetWinChance.Invoke(null, new object[] { fortTester });
        fortStatus.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 100;
        float chanceEX = (float)mGetWinChance.Invoke(null, new object[] { fortTester });
        if (Math.Abs(chanceI - 0.10f) > 0.001f || Math.Abs(chanceEX - 1.00f) > 0.001f)
            throw new InvalidOperationException($"SnowQueen Fortitude win chances unexpected: I={chanceI}, EX={chanceEX}");

        sqAbn.GetType().GetMethod("FreezeCaptive", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(sqAbn, new object[] { savedCaptive });
        if (!(bool)sqAbn.GetType().GetProperty("HasFrozenCaptive", BindingFlags.Public | BindingFlags.Instance).GetValue(sqAbn)
            || savedCaptive.Spawned)
        {
            throw new InvalidOperationException("SnowQueen FreezeCaptive did not seal colonist into frozenContainer.");
        }

        if (sqAbn.CanBeStudiedNow(out string sqBlockReason) || string.IsNullOrEmpty(sqBlockReason) || sqAbn.StudiableComp.CurrentlyStudiable())
            throw new InvalidOperationException("SnowQueen failed to block normal study while a captive is frozen.");
        if (!sqAbn.GetPlatformFloatMenuOptions(fortTester).Any(opt => !opt.Disabled && opt.action != null))
            throw new InvalidOperationException("SnowQueen missing right-click Rescue Duel float menu option while captive is frozen.");

        sqPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, sqPlat.DrawPos);
        sqPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, sqPlat.DrawPos);
        sqPlat.DynamicDrawPhaseAt(DrawPhase.Draw, sqPlat.DrawPos);

        // 3g. NotesFromCrazedResearcher study modifier (+10%), safe return, 60-damage explosion, and Portrait post-intercept order
        var (notePlat, _, noteTool) = ContainToolOnPlatform(map, worker, "NotesFromCrazedResearcher", 0x4c430204);
        notePlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, notePlat.DrawPos);
        notePlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, notePlat.DrawPos);
        notePlat.DynamicDrawPhaseAt(DrawPhase.Draw, notePlat.DrawPos);

        Pawn noteScholar = SpawnValidationColonist(map, map.Center);
        float rateBeforeNote = sqAbn.CheckStudierSkillRequire(noteScholar.GetComp<CompPawnStatus>(), EAnomalyWorkType.Insight);
        noteTool.OnEquipped(noteScholar);
        float rateWithNote = sqAbn.CheckStudierSkillRequire(noteScholar.GetComp<CompPawnStatus>(), EAnomalyWorkType.Insight);
        if (rateWithNote < rateBeforeNote + 0.095f)
            throw new InvalidOperationException($"NotesFromCrazedResearcher study rate bonus missing: before={rateBeforeNote}, after={rateWithNote}");

        HediffDef noteBuffDef = DefDatabase<HediffDef>.GetNamed("LC_ResearcherNoteBuff", true);
        if (!noteScholar.health.hediffSet.HasHediff(noteBuffDef))
            throw new InvalidOperationException("NotesFromCrazedResearcher did not grant LC_ResearcherNoteBuff.");
        sqAbn.Notify_Studied(noteScholar);
        noteTool.OnUnequipped(noteScholar, 1800, returnedNormally: true);
        if (noteScholar.Dead || noteTool.IsEquipped)
            throw new InvalidOperationException("NotesFromCrazedResearcher safe return failed.");
        CleanupTestContainmentTerminal(notePlat);

        Pawn noteVictim = SpawnValidationColonist(map, FindFarthestSafeCell(map, minColonistDist: 16));
        var (noteBoomPlat, _, noteToolBoom) = ContainToolOnPlatform(map, noteVictim, "NotesFromCrazedResearcher", 0x4c430205);
        noteToolBoom.OnEquipped(noteVictim);
        noteVictim.TakeDamage(new DamageInfo(redDef, 65f, instigator: worker));
        if (!noteVictim.Dead)
            throw new InvalidOperationException("NotesFromCrazedResearcher >=60 damage threshold did not detonate wearer.");
        CleanupTestContainmentTerminal(noteBoomPlat);

        // Portrait of Another World (ILCPawnDamageInterceptor) must intercept/redirect before NotesFromCrazedResearcher (ILCPawnPostInterceptDamageHandler)
        Pawn synergyWard = SpawnValidationColonist(map, FindFarthestSafeCell(map, minColonistDist: 18));
        var (synergyPortraitPlat, _, synergyPortraitTool) = ContainToolOnPlatform(map, synergyWard, "PortraitOfAnotherWorld", 0x4c430206);
        synergyPortraitTool.OnToolUsed(synergyWard);
        Pawn synergySub = synergyPortraitTool.GetType().GetProperty("SubstitutePawn")?.GetValue(synergyPortraitTool) as Pawn;
        var (synergyNotePlat, _, synergyNoteTool) = ContainToolOnPlatform(map, synergyWard, "NotesFromCrazedResearcher", 0x4c430207);
        synergyNoteTool.OnEquipped(synergyWard);
        if (synergySub != null)
        {
            for (int hit = 0; hit < 9; hit++)
            {
                synergyWard.TakeDamage(new DamageInfo(redDef, 8f, armorPenetration: 1f, instigator: worker));
                var subInjuries = new List<Hediff_Injury>();
                synergySub.health.hediffSet.GetHediffs(ref subInjuries);
                foreach (Hediff_Injury inj in subInjuries) inj.Heal(1000f);
            }
            Hediff noteH = synergyWard.health.hediffSet.GetFirstHediffOfDef(noteBuffDef);
            object noteHc = (noteH as HediffWithComps)?.comps?.FirstOrDefault(c => c.GetType().Name == "HediffComp_ResearcherNote");
            float trackedDmg = (float)(noteHc?.GetType().GetProperty("CumulativeDamageTaken")?.GetValue(noteHc) ?? -1f);
            if (synergyWard.Dead || Math.Abs(trackedDmg) > 0.001f)
                throw new InvalidOperationException($"NotesFromCrazedResearcher counted Portrait-redirected damage! dead={synergyWard.Dead}, tracked={trackedDmg}");
        }
        synergyPortraitPlat.EjectContents();
        if (synergyNoteTool.IsEquipped) synergyNoteTool.OnUnequipped(synergyWard, 1800, returnedNormally: false);
        CleanupTestContainmentTerminal(synergyPortraitPlat);
        CleanupTestContainmentTerminal(synergyNotePlat);

        // Ensure save/reload fixture colonists start healthy and away from roaming test hostiles
        GameComponent_LC gcNow = GameComponent_LC.Instance;
        Pawn savedBlessed = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => !p.Dead && !p.Downed && p.equipment?.Primary != null && gcNow != null && gcNow.IsWeaponBlessed(p.equipment.Primary));
        if (savedBlessed == null && gcNow != null)
        {
            ThingWithComps blessedWeapon = map.listerThings.AllThings.OfType<ThingWithComps>().FirstOrDefault(t => gcNow.IsWeaponBlessed(t));
            if (blessedWeapon == null)
            {
                Pawn downedHolder = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.equipment?.Primary != null && gcNow.IsWeaponBlessed(p.equipment.Primary));
                if (downedHolder != null)
                {
                    blessedWeapon = downedHolder.equipment.Primary;
                    downedHolder.equipment.Remove(blessedWeapon);
                }
            }
            if (blessedWeapon == null)
            {
                Corpse corpseHolder = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()
                    .FirstOrDefault(c => c.InnerPawn?.equipment?.Primary != null && gcNow.IsWeaponBlessed(c.InnerPawn.equipment.Primary));
                if (corpseHolder != null)
                {
                    blessedWeapon = corpseHolder.InnerPawn.equipment.Primary;
                    corpseHolder.InnerPawn.equipment.Remove(blessedWeapon);
                }
            }
            if (blessedWeapon != null)
            {
                if (blessedWeapon.Spawned) blessedWeapon.DeSpawn();
                savedBlessed = SpawnValidationColonist(map, FindFarthestSafeCell(map));
                savedBlessed.equipment?.DestroyAllEquipment();
                savedBlessed.equipment?.AddEquipment(blessedWeapon);
            }
        }
        if (savedBlessed == null)
        {
            throw new InvalidOperationException("No live colonist holds blessed weapon before save.");
        }
        foreach (Pawn fixturePawn in new[] { savedBlessed, savedHeartBearer })
        {
            var injuries = new List<Hediff_Injury>();
            fixturePawn.health.hediffSet.GetHediffs(ref injuries);
            foreach (Hediff_Injury inj in injuries) inj.Heal(1000f);
            fixturePawn.Position = FindFarthestSafeCell(map);
            fixturePawn.Notify_Teleported(true, true);
            if (fixturePawn.drafter != null) fixturePawn.drafter.Drafted = true;
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; Phase1And2; 10_abnormalities/equippables/freischutz/snowqueen/mechanics");
    }

    private static void ValidatePhase1And2AfterLoad(Map map)
    {
        bool foundSavedHeart = false;
        bool foundSavedSnowQueen = false;
        HediffDef heartBuffDef = DefDatabase<HediffDef>.GetNamed("LC_HeartOfAspirationBuff", true);

        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "HeartOfAspiration"
                && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430202)
            {
                CompAbnormalityTool tool = plat.HeldToolComp;
                Pawn carrier = tool?.ActiveEquipper;
                if (tool == null || !tool.IsEquipped || carrier == null || carrier.Dead
                    || !carrier.health.hediffSet.HasHediff(heartBuffDef))
                {
                    throw new InvalidOperationException("Saved HeartOfAspiration active equip state did not survive save/load.");
                }

                Hediff h = carrier.health.hediffSet.GetFirstHediffOfDef(heartBuffDef);
                object hc = (h as HediffWithComps)?.comps?.FirstOrDefault(c => c.GetType().Name == "HediffComp_HeartOfAspiration");
                Thing linked = (Thing)hc?.GetType().GetField("toolThing", BindingFlags.Public | BindingFlags.Instance)?.GetValue(hc);
                if (linked != plat.HeldPawn)
                {
                    throw new InvalidOperationException("Saved HeartOfAspiration toolThing link did not survive save/load.");
                }

                foundSavedHeart = true;
            }
            else if (plat.HeldPawn?.def?.defName == "SnowQueen"
                && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430203)
            {
                CompAbnormality sq = plat.HeldPawn.GetComp<CompAbnormality>();
                bool hasFrozen = (bool)sq.GetType().GetProperty("HasFrozenCaptive", BindingFlags.Public | BindingFlags.Instance).GetValue(sq);
                Pawn captive = (Pawn)sq.GetType().GetProperty("FrozenCaptive", BindingFlags.Public | BindingFlags.Instance).GetValue(sq);
                if (!hasFrozen || captive == null || captive.Dead)
                {
                    throw new InvalidOperationException("Saved SnowQueen frozenContainer captive did not survive save/load.");
                }

                Pawn rescuer = SpawnValidationColonist(map, plat.Position);
                CompPawnStatus emp = rescuer.GetComp<CompPawnStatus>();
                emp.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 100;
                int winSeed = 1;
                for (int s = 1; s <= 30; s++)
                {
                    Rand.PushState(s);
                    bool won = Rand.Chance(0.95f);
                    Rand.PopState();
                    if (won) { winSeed = s; break; }
                }

                Rand.PushState(winSeed);
                try
                {
                    sq.GetType().GetMethod("ResolveDuel", BindingFlags.Public | BindingFlags.Instance)
                        .Invoke(sq, new object[] { rescuer });
                }
                finally { Rand.PopState(); }

                bool stillFrozen = (bool)sq.GetType().GetProperty("HasFrozenCaptive", BindingFlags.Public | BindingFlags.Instance).GetValue(sq);
                if (stillFrozen || !captive.Spawned || captive.Dead)
                {
                    throw new InvalidOperationException("SnowQueen ResolveDuel post-load failed to rescue frozen captive.");
                }

                ThingDef sqGiftDef = DefDatabase<ThingDef>.GetNamed("Accessory_SnowQueen", true);
                HediffDef sqKissDef = DefDatabase<HediffDef>.GetNamed("LC_SnowQueenKiss", true);
                bool rescuerGotGift = rescuer.apparel?.WornApparel?.Any(a => a.def == sqGiftDef) == true;
                bool captiveGotGift = captive.apparel?.WornApparel?.Any(a => a.def == sqGiftDef) == true;
                if (!rescuerGotGift || !captiveGotGift
                    || rescuer.health.hediffSet.HasHediff(sqKissDef)
                    || captive.health.hediffSet.HasHediff(sqKissDef))
                {
                    throw new InvalidOperationException($"SnowQueen duel victory gift/kiss cleanup failed: rescuerGift={rescuerGotGift}, captiveGift={captiveGotGift}");
                }

                foundSavedSnowQueen = true;
            }
        }

        if (!foundSavedHeart)
            throw new InvalidOperationException("Saved HeartOfAspiration containment terminal missing after reload.");
        if (!foundSavedSnowQueen)
            throw new InvalidOperationException("Saved SnowQueen containment terminal missing after reload.");
    }

    private static void ValidatePhase3(Map map, object abnSettings)
    {
        (string name, string cat, bool isTool)[] phase3 =
        {
            ("AllAroundHelper", "HE", false),
            ("JudgementBird", "WAW", false),
            ("BigBird", "WAW", false),
            ("LittleRedRidingHoodedMercenary", "WAW", false),
            ("BackwardClock", "WAW", true),
            ("Schadenfreude", "HE", false),
        };

        // 1. Codex, ritual extraction pool, E.G.O gear + ALEPH Pale Justitia profile, and settings toggles
        foreach (var (name, cat, isTool) in phase3)
        {
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamed(name, true);
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamed(name + "Egg", true);
            if (AbnormalityUtility.ResolveCodexEntry(raceDef)?.defName != name
                || AbnormalityUtility.ResolveCodexEntry(eggDef)?.defName != name)
            {
                throw new InvalidOperationException($"Phase3 Codex entry resolution failed for {name} / {name}Egg.");
            }

            if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual(cat).Contains(eggDef))
            {
                throw new InvalidOperationException($"Phase3 {name}Egg missing from {cat} ritual extraction list.");
            }

            if (!isTool)
            {
                DefDatabase<ThingDef>.GetNamed("EgoWeapon_" + name, true);
                DefDatabase<ThingDef>.GetNamed("Apparel_" + name + "Suit", true);
                DefDatabase<ThingDef>.GetNamed("Accessory_" + name, true);
            }

            FieldInfo toggleField = abnSettings.GetType().GetField("Enable" + name)
                ?? throw new InvalidOperationException($"Missing Phase3 setting toggle Enable{name}.");
            bool prev = (bool)toggleField.GetValue(abnSettings);
            try
            {
                toggleField.SetValue(abnSettings, false);
                if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual(cat).Contains(eggDef))
                {
                    throw new InvalidOperationException($"Enable{name}=false did not filter {name}Egg.");
                }
            }
            finally
            {
                toggleField.SetValue(abnSettings, prev);
            }
        }

        // Verify ALEPH Pale Justitia weapon & 0.5/0.5/0.5/0.5 suit + BigBird 0.4 Black suit
        ThingDef justitiaWeaponDef = DefDatabase<ThingDef>.GetNamed("EgoWeapon_JudgementBird", true);
        if (!FourColorOverrides.TryGetWeaponProfile(justitiaWeaponDef, out var justitiaWp)
            || justitiaWp.damageType != FourColorDamageType.Pale
            || justitiaWp.riskLevel != LCRiskLevel.ALEPH)
        {
            throw new InvalidOperationException($"EgoWeapon_JudgementBird expected ALEPH Pale profile, got {justitiaWp.riskLevel}/{justitiaWp.damageType}.");
        }

        ThingDef justitiaSuitDef = DefDatabase<ThingDef>.GetNamed("Apparel_JudgementBirdSuit", true);
        if (!FourColorOverrides.TryGetApparelProfile(justitiaSuitDef, out var justitiaAp)
            || justitiaAp.riskLevel != LCRiskLevel.ALEPH
            || Math.Abs(justitiaAp.pale - 0.5f) > 0.001f)
        {
            throw new InvalidOperationException($"Apparel_JudgementBirdSuit expected ALEPH 0.5 Pale resistance, got {justitiaAp.riskLevel}/{justitiaAp.pale}.");
        }

        ThingDef lampSuitDef = DefDatabase<ThingDef>.GetNamed("Apparel_BigBirdSuit", true);
        if (!FourColorOverrides.TryGetApparelProfile(lampSuitDef, out var lampAp)
            || lampAp.riskLevel != LCRiskLevel.WAW
            || Math.Abs(lampAp.black - 0.4f) > 0.001f)
        {
            throw new InvalidOperationException($"Apparel_BigBirdSuit expected WAW 0.4 Black resistance, got {lampAp.riskLevel}/{lampAp.black}.");
        }

        // 2. Standard containment, work, PeBox, E.G.O extraction, rendering, and egg-return across all 4 standard entities
        int pIndex = 0;
        foreach (var (name, _, isTool) in phase3)
        {
            if (isTool) continue;

            Pawn worker = SpawnValidationColonist(map, map.Center);
            worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
            CompPawnStatus emp = worker.GetComp<CompPawnStatus>() ?? throw new InvalidOperationException("Phase3 validation worker missing CompPawnStatus.");
            emp.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 100;
            emp.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 100;
            emp.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 100;
            emp.GetPawnStatusLevel(EPawnStatus.Justice).Status = 100;

            var plat = SpawnTestContainmentTerminal(map);
            IntVec3 cell = plat.Position;

            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg", true));
            int sig = 0x4c430300 + pIndex++;
            AbnormalityUtility.TransferSignature(egg, sig);
            if (!plat.TryCaptureThing(egg)) throw new InvalidOperationException($"Phase3 egg failed to enter room containment: {name}");

            Pawn held = plat.HeldPawn ?? throw new InvalidOperationException($"Phase3 egg failed to enter containment: {name}");
            CompAbnormality abn = held.GetComp<CompAbnormality>() ?? throw new InvalidOperationException($"Phase3 missing CompAbnormality: {name}");
            if (abn.biosignature != sig) throw new InvalidOperationException($"Phase3 identity mismatch: {name}");

            AbnormalityUtility.Discover(held.def);
            plat.GetGizmos().ToList();
            held.Drawer.renderer.EnsureGraphicsInitialized();
            plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
            plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
            plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);

            plat.CurWorkType = Enum.GetValues(typeof(EAnomalyWorkType)).Cast<EAnomalyWorkType>()
                .OrderByDescending(w => abn.CheckStudierSkillRequire(emp, w)).First();
            float rate = abn.CheckStudierSkillRequire(emp, plat.CurWorkType);
            if (rate <= 0f) throw new InvalidOperationException($"Phase3 worker cannot work on {name}: rate={rate}");

            abn.PeBoxComp.CurAmountIndiPebox = 0;
            Rand.PushState(sig);
            try
            {
                for (int i = 0; i < 24; i++) plat.Notify_StudyInterval(emp);
            }
            finally { Rand.PopState(); }
            abn.Notify_Studied(worker);
            if (abn.PeBoxComp.CurAmountIndiPebox <= 0) throw new InvalidOperationException($"Phase3 no PeBox produced by {name}");

            held.GetComp<CompAbnormalityStudiable>().Study(worker, 1000f);
            CompEgoExtractable ego = held.GetComp<CompEgoExtractable>();
            if (ego != null && ego.Props.weaponExtracted != null)
            {
                abn.PeBoxComp.CurAmountIndiPebox = 1000;
                int before = ego.CurAmountWeapon;
                typeof(CompEgoExtractable).GetMethod("ExtractEGO", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ego, new object[] { EGO_TYPE.Weapon });
                if (ego.CurAmountWeapon != before + 1) throw new InvalidOperationException($"Phase3 EGO extraction failed: {name}");
            }

            plat.EjectContents();
            var fx = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Dying" + name, true), cell, map);
            fx.InitWith(held);
            if (held.Spawned) held.DeSpawn();
            Find.WorldPawns.PassToWorld(held, PawnDiscardDecideMode.Discard);
            fx.Complete();
            Thing returnedEgg = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed(name + "Egg", true))
                .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == sig);
            if (returnedEgg == null) throw new InvalidOperationException($"Phase3 identity did not survive egg return: {name}");
            CleanupTestContainmentTerminal(plat);
            if (worker.Spawned) worker.DeSpawn();
            if (!worker.Destroyed) worker.Destroy();
        }

        // 3a. BigBird death-counter drop via Patch_PawnKill hook + Enchantment Snap-Out gizmo
        var bbPlat = SpawnTestContainmentTerminal(map);
        Thing bbEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("BigBirdEgg", true));
        if (!bbPlat.TryCaptureThing(bbEgg)) throw new InvalidOperationException("BigBirdEgg failed to enter room containment.");
        CompAbnormality bbAbn = bbPlat.HeldPawn.GetComp<CompAbnormality>();
        int bbStartQ = bbAbn.QliphothCountCurrent;
        for (int i = 0; i < 5; i++)
        {
            Pawn sacrificial = SpawnValidationColonist(map, FindFarthestSafeCell(map));
            sacrificial.Kill(null);
            if (sacrificial.Corpse != null && !sacrificial.Corpse.Destroyed) sacrificial.Corpse.Destroy();
        }
        if (bbAbn.QliphothCountCurrent != bbStartQ - 1)
            throw new InvalidOperationException($"BigBird Qliphoth counter did not drop after 5 humanlike deaths (start={bbStartQ}, cur={bbAbn.QliphothCountCurrent}).");
        CleanupTestContainmentTerminal(bbPlat);

        Pawn enchantedPawn = SpawnValidationColonist(map, map.Center);
        HediffDef enchantDef = DefDatabase<HediffDef>.GetNamed("LC_BigBirdEnchanted", true);
        Hediff enchantH = HediffMaker.MakeHediff(enchantDef, enchantedPawn);
        enchantedPawn.health.AddHediff(enchantH);
        string snapOutLabel = "LC_BigBird_SnapOutGizmoLabel".Translate();
        Command_Action snapCmd = enchantedPawn.GetGizmos().OfType<Command_Action>().FirstOrDefault(g => g.defaultLabel == snapOutLabel)
            ?? throw new InvalidOperationException("LC_BigBirdEnchanted missing Snap Out of Trance gizmo.");
        snapCmd.action();
        if (enchantedPawn.health.hediffSet.HasHediff(enchantDef))
            throw new InvalidOperationException("BigBird Snap Out gizmo failed to remove LC_BigBirdEnchanted.");
        if (enchantedPawn.Spawned) enchantedPawn.DeSpawn();
        if (!enchantedPawn.Destroyed) enchantedPawn.Destroy();

        // 3b. LittleRed mercenary hire -> allied faction + recall -> return to containment room + escape counter drop
        var lrPlat = SpawnTestContainmentTerminal(map);
        Thing lrEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LittleRedRidingHoodedMercenaryEgg", true));
        if (!lrPlat.TryCaptureThing(lrEgg)) throw new InvalidOperationException("LittleRedEgg failed to enter room containment.");
        Pawn lrPawn = lrPlat.HeldPawn;
        CompAbnormality lrAbn = lrPawn.GetComp<CompAbnormality>();
        lrAbn.PeBoxComp.CurAmountIndiPebox = 30;

        Pawn prey = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("AllAroundHelper", true), LCContainmentUtility.AbnormalityFaction);
        GenSpawn.Spawn(prey, FindFarthestSafeCell(map), map);
        bool hired = (bool)lrAbn.GetType().GetMethod("RequestMercenaryHunt", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(lrAbn, new object[] { prey });
        bool isHiredNow = (bool)lrAbn.GetType().GetProperty("IsHired", BindingFlags.Public | BindingFlags.Instance).GetValue(lrAbn);
        if (!hired || !isHiredNow || !lrPawn.Spawned || lrPawn.Faction != Faction.OfPlayer || lrAbn.PeBoxComp.CurAmountIndiPebox != 15)
            throw new InvalidOperationException($"LittleRed mercenary hire failed: hired={hired}, isHired={isHiredNow}, spawned={lrPawn.Spawned}, pe={lrAbn.PeBoxComp.CurAmountIndiPebox}");
        string recallLabel = "LC_LittleRed_RecallLabel".Translate();
        if (!lrPawn.GetGizmos().OfType<Command_Action>().Any(g => g.defaultLabel == recallLabel))
            throw new InvalidOperationException("Hired LittleRed missing Recall Mercenary gizmo.");
        bool recalled = (bool)lrAbn.GetType().GetMethod("RecallFromHunt", BindingFlags.Public | BindingFlags.Instance).Invoke(lrAbn, null);
        if (!recalled || lrPlat.HeldPawn != lrPawn || lrPawn.Faction != LCContainmentUtility.AbnormalityFaction)
            throw new InvalidOperationException("LittleRed RecallFromHunt failed to return into homePlatform.");

        int lrQBeforeEscape = lrAbn.QliphothCountCurrent;
        prey.GetComp<CompAbnormality>().Notify_Escaped();
        if (lrAbn.QliphothCountCurrent != lrQBeforeEscape - 1)
            throw new InvalidOperationException($"LittleRed Qliphoth counter did not drop when another abnormality escaped (before={lrQBeforeEscape}, after={lrAbn.QliphothCountCurrent}).");
        if (prey.Spawned) prey.DeSpawn();
        if (!prey.Destroyed) prey.Destroy();
        CleanupTestContainmentTerminal(lrPlat);

        // 3c. BackwardClock Level III charge 0->4, Level V rejection/activation, Time Rewind suppression + activator erasure
        Pawn tempWorker = SpawnValidationColonist(map, map.Center);
        var (bcPlat, _, bcTool) = ContainToolOnPlatform(map, tempWorker, "BackwardClock", 0x4c430310);
        PropertyInfo bcChargeProp = bcTool.GetType().GetProperty("ChargeLevel", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo bcRewindsProp = bcTool.GetType().GetProperty("TotalRewinds", BindingFlags.Public | BindingFlags.Instance);

        Pawn lv1User = SpawnValidationColonist(map, bcPlat.Position);
        CompPawnStatus lv1Stat = lv1User.GetComp<CompPawnStatus>();
        lv1Stat.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 10;
        lv1Stat.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 10;
        lv1Stat.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 10;
        lv1Stat.GetPawnStatusLevel(EPawnStatus.Justice).Status = 10;
        if (bcTool.CanUseTool(lv1User).Accepted)
            throw new InvalidOperationException("BackwardClock should reject Level I employee for charging.");

        Pawn lv3User = SpawnValidationColonist(map, bcPlat.Position);
        CompPawnStatus lv3Stat = lv3User.GetComp<CompPawnStatus>();
        lv3Stat.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 60;
        lv3Stat.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 60;
        lv3Stat.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 60;
        lv3Stat.GetPawnStatusLevel(EPawnStatus.Justice).Status = 60;
        if (!bcTool.CanUseTool(lv3User).Accepted)
            throw new InvalidOperationException("BackwardClock should accept Level III+ employee for charging.");

        for (int i = 0; i < 4; i++) bcTool.OnToolUsed(lv3User);
        if ((int)bcChargeProp.GetValue(bcTool) != 4)
            throw new InvalidOperationException($"BackwardClock expected charge 4/4, got {bcChargeProp.GetValue(bcTool)}.");
        // Bypass short cooldown from charging loop for direct level-gate verification
        typeof(CompAbnormalityTool).GetField("lastUsedTick", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bcTool, -999999);
        if (bcTool.CanUseTool(lv3User).Accepted)
            throw new InvalidOperationException("BackwardClock at 4/4 should reject Level III/IV employee for activation.");

        Pawn lv5User = SpawnValidationColonist(map, bcPlat.Position);
        CompPawnStatus lv5Stat = lv5User.GetComp<CompPawnStatus>();
        lv5Stat.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 100;
        lv5Stat.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 100;
        lv5Stat.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 100;
        lv5Stat.GetPawnStatusLevel(EPawnStatus.Justice).Status = 100;
        if (!bcTool.CanUseTool(lv5User).Accepted)
            throw new InvalidOperationException("BackwardClock at 4/4 should accept Level V employee for Time Rewind.");

        Pawn breachedJb = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("JudgementBird", true), LCContainmentUtility.AbnormalityFaction);
        int jbSig = breachedJb.GetComp<CompAbnormality>().biosignature;
        GenSpawn.Spawn(breachedJb, FindFarthestSafeCell(map), map);
        bcTool.OnToolUsed(lv5User);
        bcTool.TickWhileHeld();
        if (!lv5User.Destroyed || !breachedJb.Destroyed || (int)bcChargeProp.GetValue(bcTool) != 0 || (int)bcRewindsProp.GetValue(bcTool) != 1)
            throw new InvalidOperationException($"BackwardClock Time Rewind failed: activatorDestroyed={lv5User.Destroyed}, jbDestroyed={breachedJb.Destroyed}, charge={bcChargeProp.GetValue(bcTool)}, rewinds={bcRewindsProp.GetValue(bcTool)}.");
        if (!map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("JudgementBirdEgg", true)).Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == jbSig))
            throw new InvalidOperationException("BackwardClock Time Rewind did not return breached JudgementBird to its egg.");

        // Cleanup temporary Phase 3 test pawns & terminal room
        foreach (Pawn tmp in new[] { tempWorker, lv1User, lv3User })
        {
            if (tmp.Spawned) tmp.DeSpawn();
            if (!tmp.Destroyed) tmp.Destroy();
        }
        CleanupTestContainmentTerminal(bcPlat);

        // 3d. Leave one BackwardClock charged to 3/4 in a containment room for Save/Reload persistence check
        Pawn saver = SpawnValidationColonist(map, map.Center);
        CompPawnStatus saverStat = saver.GetComp<CompPawnStatus>();
        saverStat.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 60;
        saverStat.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 60;
        saverStat.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 60;
        saverStat.GetPawnStatusLevel(EPawnStatus.Justice).Status = 60;
        var (savedBcPlat, _, savedBcTool) = ContainToolOnPlatform(map, saver, "BackwardClock", 0x4c430311);
        for (int i = 0; i < 3; i++) savedBcTool.OnToolUsed(saver);
        savedBcPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, savedBcPlat.DrawPos);
        savedBcPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, savedBcPlat.DrawPos);
        savedBcPlat.DynamicDrawPhaseAt(DrawPhase.Draw, savedBcPlat.DrawPos);
        if (saver.Spawned) saver.DeSpawn();
        if (!saver.Destroyed) saver.Destroy();

        // 3e. Schadenfreude reverse camera-gaze dynamic resistance & Gaze E.G.O rending debuff
        Pawn sfPawn = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Schadenfreude", true), LCContainmentUtility.AbnormalityFaction);
        GenSpawn.Spawn(sfPawn, FindFarthestSafeCell(map), map);
        CompAbnormality sfAbn = sfPawn.GetComp<CompAbnormality>();
        MethodInfo setWatch = sfAbn.GetType().GetMethod("SetTestWatchOverride", BindingFlags.Public | BindingFlags.Instance);
        MethodInfo tickBreached = sfAbn.GetType().GetMethod("TickBreached", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo isWatchedProp = sfAbn.GetType().GetProperty("IsWatchedActive", BindingFlags.Public | BindingFlags.Instance);

        setWatch.Invoke(sfAbn, new object[] { (bool?)false });
        tickBreached.Invoke(sfAbn, null);
        if ((bool)isWatchedProp.GetValue(sfAbn))
            throw new InvalidOperationException("Schadenfreude should stay dormant (unwatched) when override=false.");
        if (!(sfPawn is ILCFourColorDynamicResistance sfDyn)
            || !sfDyn.TryGetDynamicFourColorResistance(FourColorDamageType.Red, out float sfUnwatchedRed)
            || Math.Abs(sfUnwatchedRed - 0.6f) > 0.001f)
        {
            throw new InvalidOperationException("Unwatched Schadenfreude expected 0.6 dynamic Red resistance.");
        }

        setWatch.Invoke(sfAbn, new object[] { (bool?)true });
        tickBreached.Invoke(sfAbn, null);
        if (!(bool)isWatchedProp.GetValue(sfAbn))
            throw new InvalidOperationException("Schadenfreude failed to activate when watched (override=true).");
        if (!sfDyn.TryGetDynamicFourColorResistance(FourColorDamageType.Red, out float sfWatchedRed)
            || Math.Abs(sfWatchedRed - 1.2f) > 0.001f
            || !sfDyn.TryGetDynamicFourColorResistance(FourColorDamageType.Pale, out float sfWatchedPale)
            || Math.Abs(sfWatchedPale - 1.5f) > 0.001f)
        {
            throw new InvalidOperationException("Watched Schadenfreude expected 1.2 Red / 1.5 Pale dynamic resistance.");
        }

        DamageDef gazeSawDef = DefDatabase<DamageDef>.GetNamed("LC_Gaze_SawDamage", true);
        HediffDef gazeRendDef = DefDatabase<HediffDef>.GetNamed("LC_GazeRending", true);
        sfPawn.TakeDamage(new DamageInfo(gazeSawDef, 6f));
        if (!sfPawn.health.hediffSet.HasHediff(gazeRendDef))
            throw new InvalidOperationException("DamageWorker_Gaze did not apply LC_GazeRending.");
        if (sfPawn.Spawned) sfPawn.DeSpawn();
        if (!sfPawn.Destroyed) sfPawn.Destroy();

        // 3f. JudgementBird execution gallows animation, corpse holder & cleanup
        AnimationDef hangStartAnim = DefDatabase<AnimationDef>.GetNamed("JudgementBird_VictimHangStart", true);
        AnimationDef hangLoopAnim = DefDatabase<AnimationDef>.GetNamed("JudgementBird_VictimHangLoop", true);
        if (!hangStartAnim.playWhenDowned || !hangLoopAnim.playWhenDowned)
            throw new InvalidOperationException("JudgementBird victim execution animations must set playWhenDowned=true.");

        Pawn jbTester = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("JudgementBird", true), LCContainmentUtility.AbnormalityFaction);
        IntVec3 jbCell = FindFarthestSafeCell(map);
        GenSpawn.Spawn(jbTester, jbCell, map);

        Pawn jbVictim = SpawnValidationColonist(map, FindSafePawnCell(map, jbCell));
        jbVictim.apparel?.DestroyAll();
        DamageDef paleDef = DefDatabase<DamageDef>.GetNamed("LC_PaleDamage", true);
        jbVictim.TakeDamage(new DamageInfo(paleDef, 200f, 1f, instigator: jbTester));
        if (!jbVictim.Dead || jbVictim.Corpse == null)
            throw new InvalidOperationException("JudgementBird validation lethal Pale strike failed to kill sacrificial colonist.");

        ThingDef gallowsDef = DefDatabase<ThingDef>.GetNamed("LC_JudgementGallowsFX", true);
        List<Thing> activeGallows = map.listerThings.ThingsOfDef(gallowsDef).Where(t => !t.Destroyed).ToList();
        if (activeGallows.Count != 1)
            throw new InvalidOperationException($"Expected 1 Thing_JudgementGallows after kill, found {activeGallows.Count}.");

        Thing gallows = activeGallows[0];
        if (!(gallows is IThingHolderWithDrawnPawn))
            throw new InvalidOperationException("Thing_JudgementGallows must implement IThingHolderWithDrawnPawn.");
        PropertyInfo heldCorpseProp = gallows.GetType().GetProperty("HeldCorpse", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Thing_JudgementGallows missing HeldCorpse property.");
        if (!ReferenceEquals(heldCorpseProp.GetValue(gallows), jbVictim.Corpse) || jbVictim.Corpse.Spawned)
            throw new InvalidOperationException("Thing_JudgementGallows did not capture victim corpse cleanly.");
        if (jbVictim.Drawer?.renderer?.CurAnimation != hangStartAnim)
            throw new InvalidOperationException($"Expected JudgementBird_VictimHangStart on hoisted corpse, got {jbVictim.Drawer?.renderer?.CurAnimation?.defName}.");

        // Second kill + early Destroy() test (verifies safe corpse drop on premature gallows destruction)
        Pawn jbVictim2 = SpawnValidationColonist(map, FindSafePawnCell(map, jbCell));
        jbVictim2.apparel?.DestroyAll();
        jbVictim2.TakeDamage(new DamageInfo(paleDef, 200f, 1f, instigator: jbTester));
        if (!jbVictim2.Dead || jbVictim2.Corpse == null)
            throw new InvalidOperationException("Second sacrificial colonist failed to die to JudgementBird Pale strike.");
        Thing gallows2 = map.listerThings.ThingsOfDef(gallowsDef).FirstOrDefault(t => !t.Destroyed && t != gallows)
            ?? throw new InvalidOperationException("Missing second Thing_JudgementGallows for early destroy test.");
        gallows2.Destroy();
        if (jbVictim2.Corpse.Destroyed || !jbVictim2.Corpse.Spawned)
            throw new InvalidOperationException("Early Thing_JudgementGallows.Destroy() failed to eject corpse safely onto map.");
        jbVictim2.Corpse.Destroy();

        // Remove jbTester before advancing 600 ticks so it doesn't cast extra AoE judgements during the animation timeline check
        if (jbTester.Spawned) jbTester.DeSpawn();
        if (!jbTester.Destroyed) jbTester.Destroy();

        MethodInfo gallowsTick = gallows.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Thing_JudgementGallows missing Tick method.");

        for (int t = 1; t <= 595; t++)
        {
            if (t == 1)
            {
                Find.TickManager.DoSingleTick();
            }
            else if (!gallows.Destroyed)
            {
                gallowsTick.Invoke(gallows, null);
                if (t != 160 && jbVictim.Drawer?.renderer?.renderTree != null && jbVictim.Drawer.renderer.CurAnimation != null)
                {
                    jbVictim.Drawer.renderer.renderTree.animationStartTick--;
                }
            }

            if (t == 105 || t == 135 || t == 220 || t == 548)
            {
                if (t == 220 && jbVictim.Drawer?.renderer?.CurAnimation != hangLoopAnim)
                    throw new InvalidOperationException($"Expected JudgementBird_VictimHangLoop at tick 220, got {jbVictim.Drawer?.renderer?.CurAnimation?.defName}.");
                gallows.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, gallows.DrawPos);
                gallows.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, gallows.DrawPos);
                gallows.DynamicDrawPhaseAt(DrawPhase.Draw, gallows.DrawPos);
            }
            if (t == 560)
            {
                if (jbVictim.Corpse.Destroyed || !jbVictim.Corpse.Spawned)
                    throw new InvalidOperationException("Thing_JudgementGallows did not release corpse back onto map by tick 560.");
                if (jbVictim.Drawer?.renderer?.CurAnimation != null)
                    throw new InvalidOperationException("Released corpse still had active hanging animation.");
            }
        }
        if (!gallows.Destroyed)
            throw new InvalidOperationException("Thing_JudgementGallows did not self-destroy after finishing execution animation.");
        if (!jbVictim.Corpse.Destroyed) jbVictim.Corpse.Destroy();

        Log.Message("[LC17 Validation] GAMEPLAY PASS; Phase3; 6_abnormalities/pale_justitia/bigbird_deaths/littlered_hire/backward_clock/schadenfreude");
        ValidateEgoWeaponSkillsAndAnimations(map);
        ValidateOrdealAndDepartmentExpansionPhase1(map);
    }

    private static void ValidateEgoWeaponSkillsAndAnimations(Map map)
    {
        foreach (string wDefName in new[]
        {
            "EgoWeapon_LittleRedRidingHoodedMercenary",
            "EgoWeapon_NothingThere",
            "EgoWeapon_JudgementBird",
            "EgoWeapon_BigBird",
            "EgoWeapon_BurrowingHeaven",
            "EgoWeapon_Schadenfreude",
            "EgoWeapon_PunishingBird",
            "EgoWeapon_DerFreischutz",
            "EgoWeapon_Laetitia",
            "EgoWeapon_FuneralOfDeadButterflies",
            "EgoWeapon_AllAroundHelper",
            "EgoWeapon_SnowQueen",
            "EgoWeapon_WarmHeartedWoodsman",
            "EgoWeapon_ScarecrowSearchingForWisdom",
            "EgoWeapon_ScorchedGirl",
            "EgoWeapon_VoidDream",
            "EgoWeapon_Ppodae",
            "EgoWeapon_FragmentOfTheUniverse",
            "EgoWeapon_MeatLantern",
            "EgoWeapon_OneSin",
            "EgoWeapon_FairyFestival",
            "EgoWeapon_WellCheers"
        })
        {
            if (!EgoWeaponAnimationManager.TryGetHandler(wDefName, out _))
                throw new InvalidOperationException($"Missing registered IEgoWeaponAnimationHandler for {wDefName}.");
        }

        // Red-X regression check: any Ethereal FX/Runner without graphicData and with drawerType != None MUST override DrawAt(Vector3, bool)
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.category == ThingCategory.Ethereal
                && def.graphicData == null
                && def.drawerType != DrawerType.None
                && (def.defName.StartsWith("LC_") || def.defName.Contains("SkillRunner")))
            {
                MethodInfo drawAt = def.thingClass?.GetMethod("DrawAt", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (drawAt == null || drawAt.DeclaringType == typeof(Thing))
                {
                    throw new InvalidOperationException(
                        $"Red-X risk on Ethereal '{def.defName}': graphicData is null, drawerType={def.drawerType}, and {def.thingClass?.FullName} does not override DrawAt.");
                }
            }
        }

        Pawn wielder = SpawnValidationColonist(map, FindFarthestSafeCell(map, minColonistDist: 20));
        wielder.drafter.Drafted = true;
        wielder.drafter.FireAtWill = false;
        wielder.skills.GetSkill(SkillDefOf.Melee).Level = 15;
        wielder.skills.GetSkill(SkillDefOf.Shooting).Level = 15;

        void SuppressNearbyExternalHostiles()
        {
            foreach (Pawn other in map.mapPawns.AllPawnsSpawned)
            {
                if (other != null && other != wielder && !other.Dead && other.Faction != Faction.OfPlayer &&
                    other.HostileTo(wielder))
                {
                    other.stances?.stunner?.StunFor(1000, wielder, addBattleLog: false);
                }
            }
        }
        SuppressNearbyExternalHostiles();

        PawnKindDef dummyKind = DefDatabase<PawnKindDef>.AllDefsListForReading
            .FirstOrDefault(k => k.RaceProps?.Animal == true && !k.RaceProps.IsMechanoid && k.RaceProps.baseBodySize >= 1.5f)
            ?? DefDatabase<PawnKindDef>.AllDefsListForReading.First(k => k.RaceProps?.Animal == true && !k.RaceProps.IsMechanoid);

        IntVec3 FindAdjacentDummyCell(IntVec3 origin)
        {
            foreach (IntVec3 c in GenAdjFast.AdjacentCells8Way(origin))
            {
                if (c.InBounds(map) && !c.Fogged(map) && c.Standable(map) &&
                    c.GetRegion(map) != null && c.GetEdifice(map) == null &&
                    GenSight.LineOfSight(origin, c, map, skipFirstCell: true))
                {
                    return c;
                }
            }
            return FindSafePawnCell(map, origin);
        }

        IntVec3 FindMidRangeTargetCell(IntVec3 origin)
        {
            foreach (IntVec3 c in GenRadial.RadialCellsAround(origin, 8f, false))
            {
                int distSq = (c - origin).LengthHorizontalSquared;
                if (distSq >= 25 && distSq <= 64 &&
                    c.InBounds(map) && !c.Fogged(map) && c.Standable(map) &&
                    c.GetRegion(map) != null && c.GetEdifice(map) == null &&
                    GenSight.LineOfSight(origin, c, map, skipFirstCell: true))
                {
                    return c;
                }
            }
            return FindSafePawnCell(map, origin + new IntVec3(6, 0, 0));
        }

        Pawn SpawnHostileDummy(IntVec3 cell)
        {
            Pawn p = PawnGenerator.GeneratePawn(dummyKind, null);
            GenSpawn.Spawn(p, cell, map);
            p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, forced: true);
            p.stances?.stunner?.StunFor(350, wielder, addBattleLog: false);
            return p;
        }

        void CleanupPawn(Pawn p)
        {
            if (p == null) return;
            if (p.Corpse != null && !p.Corpse.Destroyed) p.Corpse.Destroy();
            if (p.Spawned) p.DeSpawn();
            if (!p.Destroyed) p.Destroy();
        }

        string[] fxDefNames =
        {
            "LC_LittleRedSlashFX", "LC_LittleRedShotgunConeFX", "LC_LittleRedThrownSickleFX", "LC_LittleRedImpactFX", "LC_CrimsonScarSkillRunnerFX",
            "LC_MimicrySlashFX", "LC_MimicryGroundRuptureFX", "LC_MimicryChargeAuraFX",
            "LC_JustitiaSlashFX", "LC_JudgementPaleWaveFX", "LC_JustitiaChargeSigilFX", "LC_JustitiaVerdictRunnerFX",
            "LC_BigBirdEnchantWaveFX", "LC_BigBirdEyeMarkFX", "LC_BigBirdBiteBurstFX", "LC_LampImpactFX", "LC_LampSkillBeaconFX", "LC_LampSkillRunnerFX",
            "LC_HeavenThrustFX", "LC_HeavenChargeAuraFX", "LC_HeavenSkillRunnerFX",
            "LC_SchadenfreudeKeyholePulseFX", "LC_SchadenfreudeBloodBurstFX", "LC_GazeSawSlashFX", "LC_GazeFlurryFX", "LC_GazeSkillRunnerFX",
            "LC_BeakTracerFX", "LC_BeakImpactFX", "LC_BeakSkillRunnerFX",
            "LC_MagicBulletBeamFX", "LC_MagicBulletPortalFX", "LC_MagicBulletHitFX", "LC_MagicBulletSkillRunnerFX",
            "LC_LaetitiaHeartBurstFX", "LC_LaetitiaGiftChargeFX", "LC_LaetitiaRibbonTracerFX", "LC_LaetitiaSkillRunnerFX",
            "LC_SolemnLamentSkillRunnerFX", "LC_GrinderMk4SkillRunnerFX", "LC_FrostSplinterSkillRunnerFX",
            "LC_LoggingSkillRunnerFX", "LC_HarvestSkillRunnerFX", "LC_FourthMatchSkillRunnerFX",
            "LC_VoidDreamCrowWaveFX", "LC_VoidDreamNightmareBurstFX", "LC_VoidDreamSkillRunnerFX",
            "LC_PpodaeSlamFX", "LC_SoCuteSkillRunnerFX",
            "LC_CosmosSongWaveFX", "LC_CosmosSpearSkillRunnerFX",
            "LC_MeatLanternChompFX",
            "LC_PenitenceHaloFX", "LC_PenitenceChargeAuraFX", "LC_PenitenceSkillRunnerFX",
            "LC_WingbeatSlashFX", "LC_WingbeatLifestealFX", "LC_WingbeatSkillRunnerFX",
            "LC_WellCheersMuzzleFizzFX", "LC_WellCheersSodaSplashFX", "LC_SodaSkillRunnerFX"
        };

        string[] runnerDefNames =
        {
            "LC_CrimsonScarSkillRunnerFX",
            "LC_JustitiaVerdictRunnerFX",
            "LC_LampSkillRunnerFX",
            "LC_HeavenSkillRunnerFX",
            "LC_GazeSkillRunnerFX",
            "LC_BeakSkillRunnerFX",
            "LC_MagicBulletSkillRunnerFX",
            "LC_LaetitiaSkillRunnerFX",
            "LC_SolemnLamentSkillRunnerFX",
            "LC_GrinderMk4SkillRunnerFX",
            "LC_FrostSplinterSkillRunnerFX",
            "LC_LoggingSkillRunnerFX",
            "LC_HarvestSkillRunnerFX",
            "LC_FourthMatchSkillRunnerFX",
            "LC_VoidDreamSkillRunnerFX",
            "LC_SoCuteSkillRunnerFX",
            "LC_CosmosSpearSkillRunnerFX",
            "LC_PenitenceSkillRunnerFX",
            "LC_WingbeatSkillRunnerFX",
            "LC_SodaSkillRunnerFX"
        };

        bool HasActiveSkillRunners()
        {
            foreach (string runnerDefName in runnerDefNames)
            {
                ThingDef runnerDef = DefDatabase<ThingDef>.GetNamedSilentFail(runnerDefName);
                if (runnerDef != null && map.listerThings.ThingsOfDef(runnerDef).Count > 0)
                    return true;
            }
            return false;
        }

        void PrepareWielderForCast()
        {
            if (wielder.Dead || wielder.Destroyed || !wielder.Spawned || wielder.mindState == null)
            {
                throw new InvalidOperationException("Validation wielder was killed or despawned by an external map hazard during ValidateEgoWeaponSkillsAndAnimations.");
            }
            wielder.pather?.StopDead();
            wielder.jobs?.StopAll(false);
            wielder.stances?.CancelBusyStanceHard();
            EgoWeaponAnimationManager.Stop(wielder);
        }

        void RenderAndWaitForSkill(Pawn target, string expectedWarmupClip, string expectedExecClip, int maxTicks = 260)
        {
            bool sawWarmup = string.IsNullOrEmpty(expectedWarmupClip);
            bool sawExec = false;
            bool animClearedAfterExec = false;
            int hediffsBefore = target.health?.hediffSet?.hediffs?.Count ?? 0;
            int missingBefore = target.health?.hediffSet?.hediffs?.Count(h => h is Hediff_MissingPart) ?? 0;

            for (int tick = 0; tick < maxTicks; tick++)
            {
                Find.TickManager.DoSingleTick();

                if (!sawWarmup && EgoWeaponAnimationManager.IsPlaying(wielder, expectedWarmupClip))
                    sawWarmup = true;
                if (!sawExec && EgoWeaponAnimationManager.IsPlaying(wielder, expectedExecClip))
                    sawExec = true;

                // Keep Wait_Combat from triggering normal melee/ranged attacks mid-skill combo
                if (sawExec && !(wielder.stances?.curStance is Stance_Warmup) &&
                    wielder.stances?.curStance is Stance_Mobile &&
                    EgoWeaponAnimationManager.IsPlaying(wielder))
                {
                    wielder.stances.SetStance(new Stance_Cooldown(2, new LocalTargetInfo(target.PositionHeld), null));
                }

                if (tick == 1 || tick % 10 == 0 || (sawExec && tick % 5 == 0))
                {
                    wielder.Drawer.renderer.EnsureGraphicsInitialized();
                    foreach (Rot4 facing in new[] { Rot4.East, Rot4.West })
                    {
                        wielder.Rotation = facing;
                        wielder.Drawer.renderer.RenderPawnAt(wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.Draw, wielder.DrawPos);
                    }

                    foreach (string fxDefName in fxDefNames)
                    {
                        ThingDef fxDef = DefDatabase<ThingDef>.GetNamedSilentFail(fxDefName);
                        if (fxDef == null) continue;
                        List<Thing> spawnedFx = map.listerThings.ThingsOfDef(fxDef);
                        for (int k = 0; k < spawnedFx.Count; k++)
                        {
                            Thing fx = spawnedFx[k];
                            if (fx == null || fx.Destroyed || !fx.Spawned) continue;
                            fx.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, fx.DrawPos);
                            fx.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, fx.DrawPos);
                            fx.DynamicDrawPhaseAt(DrawPhase.Draw, fx.DrawPos);
                        }
                    }
                }

                if (sawExec && !EgoWeaponAnimationManager.IsPlaying(wielder) && !HasActiveSkillRunners())
                {
                    animClearedAfterExec = true;
                    break;
                }
            }

            if (!sawWarmup)
                throw new InvalidOperationException($"Expected warmup animation '{expectedWarmupClip}' did not start on {wielder.equipment?.Primary?.def?.defName}.");
            if (!sawExec)
                throw new InvalidOperationException($"Expected execution animation '{expectedExecClip}' did not start on {wielder.equipment?.Primary?.def?.defName}.");
            if (!animClearedAfterExec || EgoWeaponAnimationManager.IsPlaying(wielder) || HasActiveSkillRunners())
                throw new InvalidOperationException($"Execution animation '{expectedExecClip}' or skill runner did not expire cleanly within {maxTicks} ticks.");
            int hediffsAfter = target.health?.hediffSet?.hediffs?.Count ?? 0;
            int missingAfter = target.health?.hediffSet?.hediffs?.Count(h => h is Hediff_MissingPart) ?? 0;
            float hpPctAfter = target.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            bool targetAffected = target.Dead || hpPctAfter < 0.999f || missingAfter > missingBefore || hediffsAfter > hediffsBefore;
            if (!targetAffected)
                throw new InvalidOperationException($"Expected target '{target.LabelShort}' to take damage from '{expectedExecClip}' (hpPct={hpPctAfter:F3}, hediffs={hediffsBefore}->{hediffsAfter}, missing={missingBefore}->{missingAfter}).");
        }

        void ValidateNormalMeleeGuarantee(ThingWithComps weapon, string expectedClipPrefix, ref Pawn targetDummy, bool staggerTargetBetweenSwings = true)
        {
            FieldInfo curMeleeVerbField = typeof(Pawn_MeleeVerbs).GetField("curMeleeVerb", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Pawn_MeleeVerbs.curMeleeVerb field not found.");

            // 1. Ensure terrain tools (4% dirt/sand kicks) are completely suppressed while holding an E.G.O. weapon
            List<VerbEntry> terrainVerbs = wielder.meleeVerbs.GetUpdatedAvailableVerbsList(terrainTools: true);
            if (terrainVerbs.Count != 0)
                throw new InvalidOperationException($"Expected 0 terrain melee verbs while wielding {weapon.def.defName}, got {terrainVerbs.Count}.");

            // 2. Ensure unarmed body verbs (fist/head/teeth) are filtered out and only primary E.G.O. weapon verbs remain
            List<VerbEntry> availableVerbs = wielder.meleeVerbs.GetUpdatedAvailableVerbsList(terrainTools: false);
            if (availableVerbs.Count == 0 || availableVerbs.Any(ve => ve.verb == null || ve.verb.EquipmentSource != weapon))
                throw new InvalidOperationException($"Non-weapon melee verb leaked into available list for {weapon.def.defName}.");

            // 3. Re-roll verb selection 40 times across cache invalidations to confirm 100% weapon selection
            for (int roll = 0; roll < 40; roll++)
            {
                curMeleeVerbField.SetValue(wielder.meleeVerbs, null);
                Verb chosen = wielder.meleeVerbs.TryGetMeleeVerb(targetDummy);
                if (chosen == null || chosen.EquipmentSource != weapon)
                    throw new InvalidOperationException($"Roll {roll}: TryGetMeleeVerb returned non-weapon verb '{chosen}' for {weapon.def.defName}.");
            }

            // 4. Execute 6 consecutive normal melee swings (covering hit/miss/dodge/lethal paths) and verify every swing plays custom animation
            for (int swing = 0; swing < 6; swing++)
            {
                if (targetDummy == null || targetDummy.Dead || !targetDummy.Spawned || targetDummy.Destroyed)
                {
                    CleanupPawn(targetDummy);
                    targetDummy = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
                }

                PrepareWielderForCast();
                EgoWeaponAnimationManager.Stop(wielder);
                if (!wielder.meleeVerbs.TryMeleeAttack(targetDummy))
                    throw new InvalidOperationException($"Normal TryMeleeAttack failed on swing {swing} for {weapon.def.defName}.");

                if (!EgoWeaponAnimationManager.IsPlaying(wielder, expectedClipPrefix))
                {
                    string activeClipId = EgoWeaponAnimationManager.TryGetActive(wielder, out var act) ? (act?.ClipId ?? "<none>") : "<none>";
                    throw new InvalidOperationException($"Normal melee swing {swing} on {weapon.def.defName} failed to trigger animation prefix '{expectedClipPrefix}' (active={activeClipId}).");
                }

                if (staggerTargetBetweenSwings)
                {
                    for (int t = 0; t < 2; t++)
                    {
                        Find.TickManager.DoSingleTick();
                    }
                }

                wielder.Drawer.renderer.EnsureGraphicsInitialized();
                foreach (Rot4 facing in new[] { Rot4.East, Rot4.West })
                {
                    wielder.Rotation = facing;
                    wielder.Drawer.renderer.RenderPawnAt(wielder.DrawPos);
                    wielder.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, wielder.DrawPos);
                    wielder.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, wielder.DrawPos);
                    wielder.DynamicDrawPhaseAt(DrawPhase.Draw, wielder.DrawPos);
                }
            }

            PrepareWielderForCast();
            EgoWeaponAnimationManager.Stop(wielder);
        }

        void ValidateRangedShotAnim(ThingWithComps weapon, string expectedShotClip, ref Pawn targetDummy, int maxTicks = 220, string expectedWarmupClip = null)
        {
            if (targetDummy == null || targetDummy.Dead || !targetDummy.Spawned || targetDummy.Destroyed)
            {
                CleanupPawn(targetDummy);
                targetDummy = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            }

            PrepareWielderForCast();
            EgoWeaponAnimationManager.Stop(wielder);
            Verb shotVerb = wielder.CurrentEffectiveVerb
                ?? throw new InvalidOperationException($"{weapon.def.defName} missing CurrentEffectiveVerb.");
            if (!shotVerb.TryStartCastOn(new LocalTargetInfo(targetDummy), false))
                throw new InvalidOperationException($"{weapon.def.defName} normal ranged TryStartCastOn failed.");

            bool sawWarmup = string.IsNullOrEmpty(expectedWarmupClip);
            bool sawShot = false;
            for (int tick = 0; tick < maxTicks; tick++)
            {
                Find.TickManager.DoSingleTick();
                if (!sawWarmup && EgoWeaponAnimationManager.IsPlaying(wielder, expectedWarmupClip))
                    sawWarmup = true;
                if (!sawShot && EgoWeaponAnimationManager.IsPlaying(wielder, expectedShotClip))
                    sawShot = true;

                if (tick == 1 || tick % 10 == 0 || (sawShot && tick % 5 == 0))
                {
                    wielder.Drawer.renderer.EnsureGraphicsInitialized();
                    foreach (Rot4 facing in new[] { Rot4.East, Rot4.West })
                    {
                        wielder.Rotation = facing;
                        wielder.Drawer.renderer.RenderPawnAt(wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.Draw, wielder.DrawPos);
                    }
                }

                if (sawShot && !EgoWeaponAnimationManager.IsPlaying(wielder))
                    break;
            }

            if (!sawWarmup)
                throw new InvalidOperationException($"{weapon.def.defName} normal ranged warmup did not trigger '{expectedWarmupClip}'.");
            if (!sawShot)
            {
                string activeClip = EgoWeaponAnimationManager.TryGetActive(wielder, out var act) ? (act?.ClipId ?? "<none>") : "<none>";
                string stanceName = wielder.stances?.curStance?.GetType().Name ?? "<null>";
                bool canHit = shotVerb.CanHitTarget(new LocalTargetInfo(targetDummy));
                throw new InvalidOperationException(
                    $"{weapon.def.defName} normal ranged shot did not trigger '{expectedShotClip}' " +
                    $"(verb={shotVerb.GetType().Name}, stance={stanceName}, clip={activeClip}, canHit={canHit}, " +
                    $"casterDown={wielder.Downed}, targetDead={targetDummy?.Dead}, targetDown={targetDummy?.Downed}, " +
                    $"dist={(targetDummy != null ? wielder.Position.DistanceTo(targetDummy.Position) : -1f):F1}).");
            }
        }

        Pawn csCloseTarget = null;
        Pawn csMidTarget = null;
        Pawn csNormalMeleeTarget = null;
        Pawn csNormalRangedTarget = null;
        Pawn ntNormalTarget = null;
        Pawn ntTarget = null;
        Pawn jbNormalTarget = null;
        Pawn jbTarget = null;
        Pawn lampNormalTarget = null;
        Pawn lampTarget = null;
        Pawn hvNormalTarget = null;
        Pawn hvTarget = null;
        Pawn gzNormalTarget = null;
        Pawn gzTarget = null;
        Pawn bkMeleeTarget = null;
        Pawn bkRangedTarget = null;
        Pawn bkSkillTarget = null;
        Pawn mbMeleeTarget = null;
        Pawn mbRangedTarget = null;
        Pawn mbSkillTarget = null;
        Pawn laMeleeTarget = null;
        Pawn laRangedTarget = null;
        Pawn laSkillTarget = null;
        Pawn currentDummy = null;
        try
        {
            // 1. Crimson Scar: normal melee & ranged shots + close-range combo + mid-range combo
            ThingWithComps crimsonWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_LittleRedRidingHoodedMercenary", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(crimsonWeapon);
            Ability csAbility = crimsonWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_LittleRedRidingHoodedMercenary missing CrimsonScar_HuntBarrage ability.");
            if (csAbility.def.defName != "CrimsonScar_HuntBarrage")
                throw new InvalidOperationException($"Unexpected Crimson Scar ability: {csAbility.def.defName}.");

            csNormalMeleeTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(crimsonWeapon, "CrimsonScar_MeleeSlash", ref csNormalMeleeTarget);
            CleanupPawn(csNormalMeleeTarget);
            csNormalMeleeTarget = null;

            csNormalRangedTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            PrepareWielderForCast();
            Verb csShotVerb = wielder.CurrentEffectiveVerb
                ?? throw new InvalidOperationException("EgoWeapon_LittleRedRidingHoodedMercenary missing CurrentEffectiveVerb.");
            if (!csShotVerb.TryStartCastOn(new LocalTargetInfo(csNormalRangedTarget), false))
                throw new InvalidOperationException("Crimson Scar normal ranged TryStartCastOn failed.");
            bool sawNormalRangeShot = false;
            for (int tick = 0; tick < 120; tick++)
            {
                Find.TickManager.DoSingleTick();
                if (!sawNormalRangeShot && EgoWeaponAnimationManager.IsPlaying(wielder, "CrimsonScar_RangeShot"))
                {
                    sawNormalRangeShot = true;
                }
                if (tick == 1 || tick % 10 == 0)
                {
                    wielder.Drawer.renderer.EnsureGraphicsInitialized();
                    foreach (Rot4 facing in new[] { Rot4.East, Rot4.West })
                    {
                        wielder.Rotation = facing;
                        wielder.Drawer.renderer.RenderPawnAt(wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, wielder.DrawPos);
                        wielder.DynamicDrawPhaseAt(DrawPhase.Draw, wielder.DrawPos);
                    }
                }
                if (sawNormalRangeShot && !EgoWeaponAnimationManager.IsPlaying(wielder))
                {
                    break;
                }
            }
            if (!sawNormalRangeShot)
                throw new InvalidOperationException("Crimson Scar normal ranged shot did not trigger CrimsonScar_RangeShot animation.");
            CleanupPawn(csNormalRangedTarget);
            csNormalRangedTarget = null;

            csCloseTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            PrepareWielderForCast();
            if (!csAbility.verb.TryStartCastOn(new LocalTargetInfo(csCloseTarget), false))
                throw new InvalidOperationException("CrimsonScar_HuntBarrage (close) TryStartCastOn failed.");
            RenderAndWaitForSkill(csCloseTarget, "CrimsonScar_SkillWarmup", "CrimsonScar_SkillClose", 260);
            CleanupPawn(csCloseTarget);
            csCloseTarget = null;

            // Mid-range barrage test
            csAbility.ResetCooldown();
            csMidTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            PrepareWielderForCast();
            if (!csAbility.verb.TryStartCastOn(new LocalTargetInfo(csMidTarget), false))
                throw new InvalidOperationException("CrimsonScar_HuntBarrage (ranged) TryStartCastOn failed.");
            RenderAndWaitForSkill(csMidTarget, "CrimsonScar_SkillWarmup", "CrimsonScar_SkillRanged", 260);
            CleanupPawn(csMidTarget);
            csMidTarget = null;

            // 2. Mimicry: normal cleave guarantee + warmup hoist -> slam + ground rupture
            PrepareWielderForCast();
            ThingWithComps mimicryWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_NothingThere", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(mimicryWeapon);
            ntNormalTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(mimicryWeapon, "Mimicry_", ref ntNormalTarget);
            CleanupPawn(ntNormalTarget);
            ntNormalTarget = null;

            Ability ntAbility = mimicryWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_NothingThere missing Mimicry_Goodbye ability.");
            ntTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            PrepareWielderForCast();
            if (!ntAbility.verb.TryStartCastOn(new LocalTargetInfo(ntTarget), false))
                throw new InvalidOperationException("Mimicry_Goodbye TryStartCastOn failed.");
            RenderAndWaitForSkill(ntTarget, "Mimicry_GoodbyeWindup", "Mimicry_GoodbyeSlam", 260);
            CleanupPawn(ntTarget);
            ntTarget = null;

            // 3. Justitia: normal slash guarantee + warmup golden scale charge -> 10-beat Pale Judgement flurry
            PrepareWielderForCast();
            ThingWithComps justitiaWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_JudgementBird", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(justitiaWeapon);
            jbNormalTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(justitiaWeapon, "Justitia_NormalSlash", ref jbNormalTarget);
            CleanupPawn(jbNormalTarget);
            jbNormalTarget = null;

            Ability jbAbility = justitiaWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_JudgementBird missing Justitia_PaleJudgement ability.");
            if (jbAbility.def.defName != "Justitia_PaleJudgement")
                throw new InvalidOperationException($"Unexpected Justitia ability: {jbAbility.def.defName}.");
            jbTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            PrepareWielderForCast();
            if (!jbAbility.verb.TryStartCastOn(new LocalTargetInfo(jbTarget), false))
                throw new InvalidOperationException("Justitia_PaleJudgement TryStartCastOn failed.");
            RenderAndWaitForSkill(jbTarget, "Justitia_VerdictCharge", "Justitia_VerdictFlurry", 260);
            CleanupPawn(jbTarget);
            jbTarget = null;

            // 4. Big Bird (Lamp): normal smash + Lantern of Salvation skill
            PrepareWielderForCast();
            ThingWithComps lampWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_BigBird", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(lampWeapon);
            lampNormalTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(lampWeapon, "Lamp_NormalSmash", ref lampNormalTarget);
            CleanupPawn(lampNormalTarget);
            lampNormalTarget = null;

            Ability lampAbility = lampWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_BigBird missing Lamp_LanternSalvation ability.");
            if (lampAbility.def.defName != "Lamp_LanternSalvation")
                throw new InvalidOperationException($"Unexpected BigBird ability: {lampAbility.def.defName}.");
            lampTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            PrepareWielderForCast();
            if (!lampAbility.verb.TryStartCastOn(new LocalTargetInfo(lampTarget), false))
                throw new InvalidOperationException("Lamp_LanternSalvation TryStartCastOn failed.");
            RenderAndWaitForSkill(lampTarget, "Lamp_SkillWarmup", "Lamp_SkillSlam", 260);
            CleanupPawn(lampTarget);
            lampTarget = null;

            // 5. Burrowing Heaven (Heaven): normal thrust + Erupting Root Impale skill
            PrepareWielderForCast();
            ThingWithComps heavenWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_BurrowingHeaven", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(heavenWeapon);
            hvNormalTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(heavenWeapon, "Heaven_NormalThrust", ref hvNormalTarget);
            CleanupPawn(hvNormalTarget);
            hvNormalTarget = null;

            Ability hvAbility = heavenWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_BurrowingHeaven missing Heaven_RootImpale ability.");
            if (hvAbility.def.defName != "Heaven_RootImpale")
                throw new InvalidOperationException($"Unexpected Heaven ability: {hvAbility.def.defName}.");
            hvTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            PrepareWielderForCast();
            if (!hvAbility.verb.TryStartCastOn(new LocalTargetInfo(hvTarget), false))
                throw new InvalidOperationException("Heaven_RootImpale TryStartCastOn failed.");
            RenderAndWaitForSkill(hvTarget, "Heaven_SkillWarmup", "Heaven_SkillSkewer", 260);
            CleanupPawn(hvTarget);
            hvTarget = null;

            // 6. Schadenfreude (Gaze): normal grind + Keyhole Sawstorm skill
            PrepareWielderForCast();
            ThingWithComps gazeWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_Schadenfreude", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(gazeWeapon);
            gzNormalTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(gazeWeapon, "Gaze_NormalGrind", ref gzNormalTarget);
            CleanupPawn(gzNormalTarget);
            gzNormalTarget = null;

            Ability gzAbility = gazeWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_Schadenfreude missing Gaze_KeyholeSawstorm ability.");
            if (gzAbility.def.defName != "Gaze_KeyholeSawstorm")
                throw new InvalidOperationException($"Unexpected Gaze ability: {gzAbility.def.defName}.");
            gzTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            PrepareWielderForCast();
            if (!gzAbility.verb.TryStartCastOn(new LocalTargetInfo(gzTarget), false))
                throw new InvalidOperationException("Gaze_KeyholeSawstorm TryStartCastOn failed.");
            RenderAndWaitForSkill(gzTarget, "Gaze_SkillWarmup", "Gaze_SkillStorm", 260);
            CleanupPawn(gzTarget);
            gzTarget = null;

            // 7. Punishing Bird (Beak): melee whip + ranged shot + Punishing Barrage skill
            PrepareWielderForCast();
            ThingWithComps beakWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_PunishingBird", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(beakWeapon);
            bkMeleeTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(beakWeapon, "Beak_MeleeWhip", ref bkMeleeTarget);
            CleanupPawn(bkMeleeTarget);
            bkMeleeTarget = null;

            bkRangedTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            ValidateRangedShotAnim(beakWeapon, "Beak_RangeShot", ref bkRangedTarget, 140);
            CleanupPawn(bkRangedTarget);
            bkRangedTarget = null;

            Ability bkAbility = beakWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_PunishingBird missing Beak_PunishingBarrage ability.");
            if (bkAbility.def.defName != "Beak_PunishingBarrage")
                throw new InvalidOperationException($"Unexpected Beak ability: {bkAbility.def.defName}.");
            bkSkillTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            PrepareWielderForCast();
            if (!bkAbility.verb.TryStartCastOn(new LocalTargetInfo(bkSkillTarget), false))
                throw new InvalidOperationException("Beak_PunishingBarrage TryStartCastOn failed.");
            RenderAndWaitForSkill(bkSkillTarget, "Beak_SkillWarmup", "Beak_SkillBarrage", 260);
            CleanupPawn(bkSkillTarget);
            bkSkillTarget = null;

            // 8. Der Freischutz (Magic Bullet): melee bash + ranged warmup/shot + Magic Bullet Salvo skill
            PrepareWielderForCast();
            ThingWithComps mbWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_DerFreischutz", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(mbWeapon);
            mbMeleeTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(mbWeapon, "MagicBullet_MeleeBash", ref mbMeleeTarget);
            CleanupPawn(mbMeleeTarget);
            mbMeleeTarget = null;

            mbRangedTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            ValidateRangedShotAnim(mbWeapon, "MagicBullet_RangeShot", ref mbRangedTarget, 220, "MagicBullet_RangeWarmup");
            CleanupPawn(mbRangedTarget);
            mbRangedTarget = null;

            Ability mbAbility = mbWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_DerFreischutz missing MagicBullet_DimensionalBarrage ability.");
            if (mbAbility.def.defName != "MagicBullet_DimensionalBarrage")
                throw new InvalidOperationException($"Unexpected MagicBullet ability: {mbAbility.def.defName}.");
            mbSkillTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            PrepareWielderForCast();
            if (!mbAbility.verb.TryStartCastOn(new LocalTargetInfo(mbSkillTarget), false))
                throw new InvalidOperationException("MagicBullet_DimensionalBarrage TryStartCastOn failed.");
            RenderAndWaitForSkill(mbSkillTarget, "MagicBullet_SkillWarmup", "MagicBullet_SkillSalvo", 260);
            CleanupPawn(mbSkillTarget);
            mbSkillTarget = null;

            // 9. Laetitia: melee bash + ranged shot + redesigned SendTheGift on non-humanlike dummy + Heartbreak without spiders
            SuppressNearbyExternalHostiles();
            PrepareWielderForCast();
            ThingWithComps laWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_Laetitia", true));
            wielder.equipment.DestroyAllEquipment();
            wielder.equipment.AddEquipment(laWeapon);
            laMeleeTarget = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
            ValidateNormalMeleeGuarantee(laWeapon, "Laetitia_MeleeBash", ref laMeleeTarget);
            CleanupPawn(laMeleeTarget);
            laMeleeTarget = null;

            laRangedTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            ValidateRangedShotAnim(laWeapon, "Laetitia_RangeShot", ref laRangedTarget, 180);
            CleanupPawn(laRangedTarget);
            laRangedTarget = null;

            Ability laAbility = laWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                ?? throw new InvalidOperationException("EgoWeapon_Laetitia missing LaetitiaEGO_SendTheGift ability.");
            if (laAbility.def.defName != "LaetitiaEGO_SendTheGift")
                throw new InvalidOperationException($"Unexpected Laetitia ability: {laAbility.def.defName}.");
            ThingDef spiderSpawnerDef = DefDatabase<ThingDef>.GetNamed("SpawningLaetitiaMonster", true);
            int spidersBefore = map.listerThings.ThingsOfDef(spiderSpawnerDef).Count;
            laSkillTarget = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
            PrepareWielderForCast();
            if (!laAbility.verb.TryStartCastOn(new LocalTargetInfo(laSkillTarget), false))
                throw new InvalidOperationException("LaetitiaEGO_SendTheGift TryStartCastOn failed on non-humanlike hostile dummy.");
            RenderAndWaitForSkill(laSkillTarget, "Laetitia_SkillWarmup", "Laetitia_SkillGift", 260);

            HediffDef aggressiveGiftDef = DefDatabase<HediffDef>.GetNamed("LaetitiaGiftAggresive", true);
            if (!laSkillTarget.Dead && laSkillTarget.health?.hediffSet?.HasHediff(aggressiveGiftDef) != true)
                throw new InvalidOperationException("Expected living target to carry LaetitiaGiftAggresive after SendTheGift skill.");

            for (int followShot = 0; followShot < 6 && !laSkillTarget.Dead && laSkillTarget.health.hediffSet.HasHediff(aggressiveGiftDef); followShot++)
            {
                ValidateRangedShotAnim(laWeapon, "Laetitia_RangeShot", ref laSkillTarget, 180);
            }

            if (!laSkillTarget.Dead && laSkillTarget.health?.hediffSet?.HasHediff(aggressiveGiftDef) == true)
                throw new InvalidOperationException("Expected Laetitia rifle follow-up shots to crack and detonate LaetitiaGiftAggresive.");

            int spidersAfter = map.listerThings.ThingsOfDef(spiderSpawnerDef).Count;
            if (spidersAfter > spidersBefore)
                throw new InvalidOperationException($"Expected 0 SpawningLaetitiaMonster spawns from redesigned E.G.O. SendTheGift, found {spidersAfter - spidersBefore}.");

            CleanupPawn(laSkillTarget);
            laSkillTarget = null;

            void ValidateMeleeOnlyEgoWeapon(string weaponDefName, string meleeClipPrefix, string expectedAbilityDefName, string warmupClip, string execClip)
            {
                PrepareWielderForCast();
                ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(weaponDefName, true));
                wielder.equipment.DestroyAllEquipment();
                wielder.equipment.AddEquipment(weapon);

                currentDummy = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
                ValidateNormalMeleeGuarantee(weapon, meleeClipPrefix, ref currentDummy);
                CleanupPawn(currentDummy);
                currentDummy = null;

                Ability ability = weapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                    ?? throw new InvalidOperationException($"{weaponDefName} missing {expectedAbilityDefName} ability.");
                if (ability.def.defName != expectedAbilityDefName)
                    throw new InvalidOperationException($"Unexpected {weaponDefName} ability: {ability.def.defName}.");

                currentDummy = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
                PrepareWielderForCast();
                if (!ability.verb.TryStartCastOn(new LocalTargetInfo(currentDummy), false))
                    throw new InvalidOperationException($"{expectedAbilityDefName} TryStartCastOn failed.");
                RenderAndWaitForSkill(currentDummy, warmupClip, execClip, 260);
                CleanupPawn(currentDummy);
                currentDummy = null;
            }

            void ValidateRangedEgoWeapon(string weaponDefName, string meleeClipPrefix, string shotClip, int shotMaxTicks, string expectedAbilityDefName, string warmupClip, string execClip)
            {
                PrepareWielderForCast();
                ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(weaponDefName, true));
                wielder.equipment.DestroyAllEquipment();
                wielder.equipment.AddEquipment(weapon);

                currentDummy = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
                ValidateNormalMeleeGuarantee(weapon, meleeClipPrefix, ref currentDummy);
                CleanupPawn(currentDummy);
                currentDummy = null;

                currentDummy = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
                ValidateRangedShotAnim(weapon, shotClip, ref currentDummy, shotMaxTicks);
                CleanupPawn(currentDummy);
                currentDummy = null;

                Ability ability = weapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                    ?? throw new InvalidOperationException($"{weaponDefName} missing {expectedAbilityDefName} ability.");
                if (ability.def.defName != expectedAbilityDefName)
                    throw new InvalidOperationException($"Unexpected {weaponDefName} ability: {ability.def.defName}.");

                currentDummy = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
                PrepareWielderForCast();
                if (!ability.verb.TryStartCastOn(new LocalTargetInfo(currentDummy), false))
                    throw new InvalidOperationException($"{expectedAbilityDefName} TryStartCastOn failed.");
                RenderAndWaitForSkill(currentDummy, warmupClip, execClip, 260);
                CleanupPawn(currentDummy);
                currentDummy = null;
            }

            // 10-13: Solemn Lament, Grinder Mk4, Frost Splinter, Logging
            SuppressNearbyExternalHostiles();
            ValidateRangedEgoWeapon("EgoWeapon_FuneralOfDeadButterflies", "SolemnLament_MeleeWhip", "SolemnLament_RangeShot", 200, "SolemnLament_ButterflyRequiem", "SolemnLament_SkillWarmup", "SolemnLament_SkillRequiem");
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_AllAroundHelper", "GrinderMk4_NormalSaw", "GrinderMk4_OverdriveRush", "GrinderMk4_SkillWarmup", "GrinderMk4_SkillRush");
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_SnowQueen", "FrostSplinter_NormalThrust", "FrostSplinter_GlacialDuel", "FrostSplinter_SkillWarmup", "FrostSplinter_SkillPierce");
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_WarmHeartedWoodsman", "Logging_NormalChop", "Logging_HeartCleave", "Logging_SkillWarmup", "Logging_SkillCleave");

            // 14-17: Harvest, Fourth Match Flame, Void Dream, So Cute!!!
            SuppressNearbyExternalHostiles();
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_ScarecrowSearchingForWisdom", "Harvest_NormalRake", "Harvest_WisdomDrain", "Harvest_SkillWarmup", "Harvest_SkillDrain");
            ValidateRangedEgoWeapon("EgoWeapon_ScorchedGirl", "FourthMatch_MeleeBash", "FourthMatch_RangeShot", 220, "FourthMatchFlame_MatchIgnition", "FourthMatch_SkillWarmup", "FourthMatch_SkillIgnition");
            ValidateRangedEgoWeapon("EgoWeapon_VoidDream", "VoidDream_MeleeTap", "VoidDream_RangeShot", 200, "EngulfingDream_LullabyBurst", "VoidDream_SkillWarmup", "VoidDream_SkillLullaby");
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_Ppodae", "SoCute_NormalClaw", "SoCute_FrenzyMaul", "SoCute_SkillWarmup", "SoCute_SkillMaul");

            // 18-22: Cosmos Spear, Meat Lantern, Penitence, Wingbeat, Soda
            SuppressNearbyExternalHostiles();
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_FragmentOfTheUniverse", "CosmosSpear_NormalThrust", "CosmosSpear_StarlightEcho", "CosmosSpear_SkillWarmup", "CosmosSpear_SkillEcho");
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_MeatLantern", "MeatLantern_NormalChomp", "Whirlwind_OmNomNom", "MeatLantern_SkillWarmup", "MeatLantern_SkillChomp");
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_OneSin", "Penitence_NormalSwing", "Penitence_Absolution", "Penitence_SkillWarmup", "Penitence_SkillAbsolution");
            SuppressNearbyExternalHostiles();
            ValidateMeleeOnlyEgoWeapon("EgoWeapon_FairyFestival", "Wingbeat_NormalSlash", "Wingbeat_FairyFeast", "Wingbeat_SkillWarmup", "Wingbeat_SkillFeast");
            ValidateRangedEgoWeapon("EgoWeapon_WellCheers", "Soda_MeleeBash", "Soda_RangeShot", 200, "Soda_RefreshingSplash", "Soda_SkillWarmup", "Soda_SkillSplash");

            // 23. Auto-cast toggle UI eligibility, combat execution (touch, mid-range melee, ranged) & persistence fixture
            {
                GameComponent_LC gcAuto = GameComponent_LC.Instance
                    ?? throw new InvalidOperationException("GameComponent_LC.Instance unavailable during E.G.O. auto-cast validation.");

                IntVec3 FindShortMidRangeCell(IntVec3 origin)
                {
                    foreach (IntVec3 c in GenRadial.RadialCellsAround(origin, 4f, false))
                    {
                        int distSq = (c - origin).LengthHorizontalSquared;
                        if (distSq >= 4 && distSq <= 16
                            && c.InBounds(map) && !c.Fogged(map) && c.Standable(map)
                            && c.GetRegion(map) != null && c.GetEdifice(map) == null
                            && GenSight.LineOfSight(origin, c, map, skipFirstCell: true))
                        {
                            return c;
                        }
                    }

                    return FindSafePawnCell(map, origin + new IntVec3(3, 0, 0));
                }

                void HealPawnToFull(Pawn targetPawn)
                {
                    if (targetPawn?.health?.hediffSet == null) return;
                    var list = targetPawn.health.hediffSet.hediffs.ToList();
                    foreach (var h in list)
                    {
                        if (h is Hediff_Injury || h is Hediff_MissingPart || h.def.isBad)
                        {
                            targetPawn.health.RemoveHediff(h);
                        }
                    }
                }

                // 23a. Default OFF state & Gizmo bottom-right checkbox eligibility
                ThingWithComps autoNtWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_NothingThere", true));
                wielder.equipment.DestroyAllEquipment();
                wielder.equipment.AddEquipment(autoNtWeapon);
                Ability autoNtAbility = autoNtWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                    ?? throw new InvalidOperationException("Auto-cast validation: EgoWeapon_NothingThere missing Mimicry_Goodbye.");

                if (gcAuto.IsEgoWeaponAutoCastEnabled(autoNtWeapon))
                    throw new InvalidOperationException("Auto-cast state on a fresh E.G.O. weapon should default to false.");

                Command_Ability ntCmd = autoNtAbility.GetGizmos().OfType<Command_Ability>().FirstOrDefault()
                    ?? throw new InvalidOperationException("Auto-cast validation: Mimicry_Goodbye did not yield Command_Ability gizmo.");
                if (!EgoWeaponAutoCastUtility.IsEligibleCommand(ntCmd))
                    throw new InvalidOperationException("Auto-cast validation: Mimicry_Goodbye Command_Ability was not recognized as eligible.");

                Rect stdCheckbox = EgoWeaponAutoCastUtility.GetCheckboxRect(new Rect(0f, 0f, 75f, 75f), shrunk: false);
                if (stdCheckbox.x < 50f || stdCheckbox.y < 50f || stdCheckbox.xMax > 75f || stdCheckbox.yMax > 75f)
                    throw new InvalidOperationException($"Auto-cast checkbox rect out of bottom-right corner bounds: {stdCheckbox}");

                // Verify auto-cast OFF does not fire Mimicry_Goodbye during Wait_Combat
                SuppressNearbyExternalHostiles();
                currentDummy = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
                PrepareWielderForCast();
                wielder.drafter.Drafted = true;
                wielder.drafter.FireAtWill = true;
                for (int tick = 0; tick < 35; tick++)
                {
                    Find.TickManager.DoSingleTick();
                    if (autoNtAbility.Casting || autoNtAbility.CooldownTicksRemaining > 0)
                        throw new InvalidOperationException("Mimicry_Goodbye auto-cast fired while auto-cast toggle was OFF.");
                }

                // Toggle ON -> touch E.G.O. skill (Mimicry_Goodbye) auto-casts in combat
                if (!gcAuto.ToggleEgoWeaponAutoCast(autoNtWeapon) || !gcAuto.IsEgoWeaponAutoCastEnabled(autoNtWeapon))
                    throw new InvalidOperationException("ToggleEgoWeaponAutoCast failed to enable auto-cast.");

                HealPawnToFull(wielder);
                CleanupPawn(currentDummy);
                currentDummy = SpawnHostileDummy(FindAdjacentDummyCell(wielder.Position));
                PrepareWielderForCast();
                wielder.drafter.Drafted = true;
                wielder.drafter.FireAtWill = true;

                bool sawTouchAutoCast = false;
                for (int tick = 0; tick < 260; tick++)
                {
                    Find.TickManager.DoSingleTick();
                    if (autoNtAbility.Casting || autoNtAbility.CooldownTicksRemaining > 0 || EgoWeaponAnimationManager.IsPlaying(wielder, "Mimicry_Goodbye"))
                    {
                        sawTouchAutoCast = true;
                    }
                    if (sawTouchAutoCast && autoNtAbility.CooldownTicksRemaining > 0 && !autoNtAbility.Casting && !EgoWeaponAnimationManager.IsPlaying(wielder) && !HasActiveSkillRunners())
                    {
                        break;
                    }
                }
                if (!sawTouchAutoCast)
                    throw new InvalidOperationException("Mimicry_Goodbye (touch E.G.O. weapon) did not auto-cast when toggled ON in combat.");
                CleanupPawn(currentDummy);
                currentDummy = null;

                // 23b. Mid-range melee E.G.O. skill auto-cast (BigBird / Lamp_LanternSalvation, range 5.5) from 2..4 cells away
                SuppressNearbyExternalHostiles();
                ThingWithComps autoLampWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_BigBird", true));
                wielder.equipment.DestroyAllEquipment();
                wielder.equipment.AddEquipment(autoLampWeapon);
                gcAuto.SetEgoWeaponAutoCast(autoLampWeapon, true);
                Ability autoLampAbility = autoLampWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                    ?? throw new InvalidOperationException("Auto-cast validation: EgoWeapon_BigBird missing Lamp_LanternSalvation.");

                HealPawnToFull(wielder);
                currentDummy = SpawnHostileDummy(FindShortMidRangeCell(wielder.Position));
                PrepareWielderForCast();
                wielder.drafter.Drafted = true;
                wielder.drafter.FireAtWill = true;

                bool sawMidMeleeAutoCast = false;
                for (int tick = 0; tick < 260; tick++)
                {
                    Find.TickManager.DoSingleTick();
                    if (autoLampAbility.Casting || autoLampAbility.CooldownTicksRemaining > 0 || EgoWeaponAnimationManager.IsPlaying(wielder, "Lamp_Skill"))
                    {
                        sawMidMeleeAutoCast = true;
                    }
                    if (sawMidMeleeAutoCast && autoLampAbility.CooldownTicksRemaining > 0 && !autoLampAbility.Casting && !EgoWeaponAnimationManager.IsPlaying(wielder) && !HasActiveSkillRunners())
                    {
                        break;
                    }
                }
                if (!sawMidMeleeAutoCast)
                    throw new InvalidOperationException("Lamp_LanternSalvation (mid-range melee E.G.O. weapon) did not auto-cast when toggled ON in Wait_Combat.");
                CleanupPawn(currentDummy);
                currentDummy = null;

                // 23c. Ranged E.G.O. skill auto-cast (PunishingBird / Beak_PunishingBarrage, range 22)
                SuppressNearbyExternalHostiles();
                ThingWithComps autoBeakWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("EgoWeapon_PunishingBird", true));
                wielder.equipment.DestroyAllEquipment();
                wielder.equipment.AddEquipment(autoBeakWeapon);
                gcAuto.SetEgoWeaponAutoCast(autoBeakWeapon, true);
                Ability autoBeakAbility = autoBeakWeapon.GetComp<CompEquippableAbility>()?.AbilityForReading
                    ?? throw new InvalidOperationException("Auto-cast validation: EgoWeapon_PunishingBird missing Beak_PunishingBarrage.");

                HealPawnToFull(wielder);
                currentDummy = SpawnHostileDummy(FindMidRangeTargetCell(wielder.Position));
                PrepareWielderForCast();
                wielder.drafter.Drafted = true;
                wielder.drafter.FireAtWill = true;

                bool sawRangedAutoCast = false;
                for (int tick = 0; tick < 260; tick++)
                {
                    Find.TickManager.DoSingleTick();
                    if (autoBeakAbility.Casting || autoBeakAbility.CooldownTicksRemaining > 0 || EgoWeaponAnimationManager.IsPlaying(wielder, "Beak_Skill"))
                    {
                        sawRangedAutoCast = true;
                    }
                    if (sawRangedAutoCast && autoBeakAbility.CooldownTicksRemaining > 0 && !autoBeakAbility.Casting && !EgoWeaponAnimationManager.IsPlaying(wielder) && !HasActiveSkillRunners())
                    {
                        break;
                    }
                }
                if (!sawRangedAutoCast)
                    throw new InvalidOperationException("Beak_PunishingBarrage (ranged E.G.O. weapon) did not auto-cast when toggled ON in combat.");
                CleanupPawn(currentDummy);
                currentDummy = null;

                // 23d. Enable auto-cast on persistent NothingThere fixture colonist to verify Save/Reload persistence in CheckReload
                Pawn savedMimicryPawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p =>
                    p != wielder && p.equipment?.Primary?.def?.defName == "EgoWeapon_NothingThere")
                    ?? throw new InvalidOperationException("Could not find persistent EgoWeapon_NothingThere colonist for auto-cast save/reload validation.");
                gcAuto.SetEgoWeaponAutoCast(savedMimicryPawn.equipment.Primary, true);
            }
        }
        finally
        {
            CleanupPawn(wielder);
            CleanupPawn(csNormalMeleeTarget);
            CleanupPawn(csNormalRangedTarget);
            CleanupPawn(csCloseTarget);
            CleanupPawn(csMidTarget);
            CleanupPawn(ntNormalTarget);
            CleanupPawn(ntTarget);
            CleanupPawn(jbNormalTarget);
            CleanupPawn(jbTarget);
            CleanupPawn(lampNormalTarget);
            CleanupPawn(lampTarget);
            CleanupPawn(hvNormalTarget);
            CleanupPawn(hvTarget);
            CleanupPawn(gzNormalTarget);
            CleanupPawn(gzTarget);
            CleanupPawn(bkMeleeTarget);
            CleanupPawn(bkRangedTarget);
            CleanupPawn(bkSkillTarget);
            CleanupPawn(mbMeleeTarget);
            CleanupPawn(mbRangedTarget);
            CleanupPawn(mbSkillTarget);
            CleanupPawn(laMeleeTarget);
            CleanupPawn(laRangedTarget);
            CleanupPawn(laSkillTarget);
            CleanupPawn(currentDummy);
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; EgoWeaponSkillsAndAnimations; All 22 E.G.O. Weapons");
    }

    private static void ValidateOrdealAndDepartmentExpansionPhase1(Map map)
    {
        // 1. Validate all 10 Department Cores exist with real descriptions and differentiated buffs
        string[] coreDefNames =
        {
            "LC_DepartmentCore_ControlTeam",
            "LC_DepartmentCore_InformationTeam",
            "LC_DepartmentCore_TrainingTeam",
            "LC_DepartmentCore_SafetyTeam",
            "LC_DepartmentCore_CentralCommandTeam",
            "LC_DepartmentCore_WelfareTeam",
            "LC_DepartmentCore_DisciplinaryTeam",
            "LC_DepartmentCore_RecordTeam",
            "LC_DepartmentCore_ExtractionTeam",
            "LC_DepartmentCore_ArchitectureTeam"
        };

        foreach (string defName in coreDefNames)
        {
            ThingDef coreDef = DefDatabase<ThingDef>.GetNamed(defName, true);
            if (string.IsNullOrWhiteSpace(coreDef.description) || coreDef.description.Trim() == ".")
                throw new InvalidOperationException($"Department core {defName} still has placeholder description.");
        }

        // 1b. Spawn Control, Safety, Disciplinary, and Extraction cores to verify employee & facility buffs
        Pawn tester = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist,
            Faction.OfPlayer,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            forceNoBackstory: true,
            fixedBiologicalAge: 25f,
            forceBaselinerChance: 1f));
        IntVec3 testerCell = FindSafePawnCell(map, map.Center);
        GenSpawn.Spawn(tester, testerCell, map);

        var spawnedCores = new List<Building_DepartmentCore>();
        try
        {
            float baseMove = tester.GetStatValue(RimWorld.StatDefOf.MoveSpeed);

            Building_DepartmentCore SpawnCore(string defName)
            {
                var core = (Building_DepartmentCore)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName, true));
                core.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(core, FindSafeBuildingCell(map), map);
                if (core.CompPower != null) core.CompPower.PowerOn = true;
                spawnedCores.Add(core);
                return core;
            }

            Building_DepartmentCore ctrlCore = SpawnCore("LC_DepartmentCore_ControlTeam");
            ctrlCore.CompDepartmentCore.TryAssignPawn(tester);
            float ctrlMove = tester.GetStatValue(RimWorld.StatDefOf.MoveSpeed);
            if (ctrlMove <= baseMove + 0.05f)
                throw new InvalidOperationException($"ControlTeam core failed to boost MoveSpeed ({baseMove:F2} -> {ctrlMove:F2}).");
            ctrlCore.CompDepartmentCore.TryUnassignPawn(tester);

            Building_DepartmentCore infoCore = SpawnCore("LC_DepartmentCore_InformationTeam");
            infoCore.CompDepartmentCore.TryAssignPawn(tester);
            if (Math.Abs(DepartmentCoreUtility.GetStudySuccessRateBonus(tester) - 0.08f) > 0.001f)
                throw new InvalidOperationException("InformationTeam core expected +8% study success rate bonus.");
            infoCore.CompDepartmentCore.TryUnassignPawn(tester);

            Building_DepartmentCore trainCore = SpawnCore("LC_DepartmentCore_TrainingTeam");
            trainCore.CompDepartmentCore.TryAssignPawn(tester);
            if (Math.Abs(DepartmentCoreUtility.GetExpGainMultiplier(tester) - 1.5f) > 0.001f)
                throw new InvalidOperationException("TrainingTeam core expected 1.5x status XP gain multiplier.");
            trainCore.CompDepartmentCore.TryUnassignPawn(tester);

            Building_DepartmentCore safetyCore = SpawnCore("LC_DepartmentCore_SafetyTeam");
            if (safetyCore.CompDepartmentCore.Props.healMentalAmount <= 0f)
                throw new InvalidOperationException("SafetyTeam core missing positive healMentalAmount.");
            safetyCore.CompDepartmentCore.TryAssignPawn(tester);
            Hediff mental = tester.health.AddHediff(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
            mental.Severity = 0.6f;
            int safetyUsed = safetyCore.CompDepartmentCore.DoHeal(tester, 100f);
            if (safetyUsed <= 0 || mental.Severity >= 0.59f)
                throw new InvalidOperationException($"SafetyTeam DoHeal failed to restore LC_MentalDamage (used={safetyUsed}, sev={mental.Severity:F2}).");
            safetyCore.CompDepartmentCore.TryUnassignPawn(tester);

            if (DepartmentCoreUtility.GetEgoExtractionCapBonus(map) != 0)
                throw new InvalidOperationException("EGO cap bonus should be 0 before DisciplinaryTeam core spawns.");
            if (DepartmentCoreUtility.ApplyEgoExtractionCost(20, map) != 20)
                throw new InvalidOperationException("EGO cost discount should be inactive before ExtractionTeam core spawns.");

            SpawnCore("LC_DepartmentCore_DisciplinaryTeam");
            SpawnCore("LC_DepartmentCore_ExtractionTeam");
            if (DepartmentCoreUtility.GetEgoExtractionCapBonus(map) != 1)
                throw new InvalidOperationException("Powered DisciplinaryTeam core failed to grant +1 EGO extraction cap.");
            if (DepartmentCoreUtility.ApplyEgoExtractionCost(20, map) != 15)
                throw new InvalidOperationException("Powered ExtractionTeam core failed to apply -25% EGO PE-Box cost (20 -> 15).");
        }
        finally
        {
            foreach (Building_DepartmentCore core in spawnedCores)
            {
                if (core != null && !core.Destroyed)
                {
                    core.DeSpawn();
                    core.Destroy();
                }
            }
            if (tester != null && !tester.Destroyed)
            {
                if (tester.Spawned) tester.DeSpawn();
                tester.Destroy();
            }
        }

        // 2. Validate 10 Departments + coreThingDef bindings + expanded Directives in LC-Anomaly-Story (if loaded)
        Type deptDefType = GenTypes.GetTypeInAnyAssembly("LCAnomalyStory.Defs.CompanyDepartmentDef");
        if (deptDefType != null)
        {
            Type dbDept = typeof(DefDatabase<>).MakeGenericType(deptDefType);
            IList allDepts = (IList)dbDept.GetProperty("AllDefsListForReading", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (allDepts.Count < 10)
                throw new InvalidOperationException($"Expected >= 10 CompanyDepartmentDefs, found {allDepts.Count}.");
            FieldInfo coreField = deptDefType.GetField("coreThingDef", BindingFlags.Public | BindingFlags.Instance);
            foreach (object dept in allDepts)
            {
                ThingDef boundCore = (ThingDef)coreField.GetValue(dept);
                if (boundCore == null)
                    throw new InvalidOperationException($"CompanyDepartmentDef {((Def)dept).defName} has null coreThingDef.");
            }

            Type dirDefType = GenTypes.GetTypeInAnyAssembly("LCAnomalyStory.Defs.CompanyDirectiveDef");
            Type dbDir = typeof(DefDatabase<>).MakeGenericType(dirDefType);
            IList allDirs = (IList)dbDir.GetProperty("AllDefsListForReading", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            if (allDirs.Count < 12)
                throw new InvalidOperationException($"Expected >= 12 CompanyDirectiveDefs across 10 departments, found {allDirs.Count}.");
        }

        // 3. Validate Dawn (4) & Noon (4) Ordeals original textures, FourColorTargetExtension, and SoundDefs (if loaded)
        string[] dawnNoonVariants =
        {
            "AmberDawn", "CrimsonDawn", "GreenDawn", "VioletDawn",
            "CrimsonNoon", "GreenNoon", "VioletNoon", "IndigoNoon"
        };
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail("LCOrdeal_AmberDawn") != null)
        {
            foreach (string variant in dawnNoonVariants)
            {
                PawnKindDef pk = DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_" + variant, true);
                if (pk.race.GetModExtension<LCAnomalyCore.ModExtensions.FourColorTargetExtension>() == null)
                    throw new InvalidOperationException($"Ordeal race {pk.race.defName} missing FourColorTargetExtension.");
                string texBase = "Things/Pawn/Ordeals/" + variant;
                if (ContentFinder<Texture2D>.Get(texBase + "_east", reportFailure: false) == null
                    || ContentFinder<Texture2D>.Get(texBase + "_south", reportFailure: false) == null)
                {
                    throw new InvalidOperationException($"Ordeal sprite textures missing for {texBase}.");
                }
            }

            string[] requiredOrdealSounds =
            {
                "LCOrdeal_Sound_BugStart", "LCOrdeal_Sound_BugEnd",
                "LCOrdeal_Sound_CircusStart", "LCOrdeal_Sound_CircusEnd",
                "LCOrdeal_Sound_MachineStart", "LCOrdeal_Sound_MachineEnd",
                "LCOrdeal_Sound_OutterGodStart", "LCOrdeal_Sound_OutterGodEnd",
                "LCOrdeal_Sound_ScavengerStart", "LCOrdeal_Sound_ScavengerEnd",
                "LCOrdeal_Sound_Bug1Dig", "LCOrdeal_Sound_OutterGodNoonDown",
                "LCOrdeal_Sound_MachineNoonAtk", "LCOrdeal_Sound_ScavengerNoonSkill"
            };
            foreach (string sndName in requiredOrdealSounds)
            {
                SoundDef sd = DefDatabase<SoundDef>.GetNamed(sndName, true);
                if (sd.subSounds == null || sd.subSounds.Count == 0)
                    throw new InvalidOperationException($"Ordeal SoundDef {sndName} has no subSounds.");
            }
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; ExpansionPhase1; 10_dept_cores/buffs/story_10depts_12directives/dawn_noon_8art_audio_fourcolor");
        ValidateOrdealAndDepartmentExpansionPhase2(map);
    }

    private static void ValidateOrdealAndDepartmentExpansionPhase2(Map map)
    {
        // 1. Validate Manager Tactical Bullets icons, SoundDefs, unlock logic, shields, slow, and execution
        string[] bulletIcons =
        {
            "UI/Commands/Bullets/Bullet_HP",
            "UI/Commands/Bullets/Bullet_SP",
            "UI/Commands/Bullets/Bullet_RedShield",
            "UI/Commands/Bullets/Bullet_WhiteShield",
            "UI/Commands/Bullets/Bullet_BlackShield",
            "UI/Commands/Bullets/Bullet_PaleShield",
            "UI/Commands/Bullets/Bullet_Slow",
            "UI/Commands/Bullets/Bullet_Execution"
        };
        foreach (string iconPath in bulletIcons)
        {
            if (ContentFinder<Texture2D>.Get(iconPath, reportFailure: false) == null)
                throw new InvalidOperationException($"Missing Manager Bullet icon: {iconPath}");
        }

        string[] bulletSounds =
        {
            "LC_Sound_BulletHeal", "LC_Sound_BulletShield", "LC_Sound_BulletSlow",
            "LC_Sound_BulletExecution", "LC_Sound_BulletEmpty",
            "LC_Sound_ShieldHit", "LC_Sound_ShieldBroken"
        };
        foreach (string snd in bulletSounds)
        {
            SoundDef sd = DefDatabase<SoundDef>.GetNamed(snd, true);
            if (sd.subSounds == null || sd.subSounds.Count == 0)
                throw new InvalidOperationException($"Manager Bullet SoundDef {snd} has no subSounds.");
        }

        if (ManagerBulletUtility.GetMaxBulletCharges(map) != 0)
            throw new InvalidOperationException("Expected 0 bullet charges before Central/Welfare/Disciplinary cores exist.");

        var cores = new List<Building_DepartmentCore>();
        Pawn friendly = null;
        Pawn slowTarget = null;
        Pawn execTarget = null;
        try
        {
            Building_DepartmentCore SpawnCore(string defName)
            {
                var core = (Building_DepartmentCore)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName, true));
                core.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(core, FindSafeBuildingCell(map), map);
                if (core.CompPower != null) core.CompPower.PowerOn = true;
                cores.Add(core);
                return core;
            }

            Building_DepartmentCore central = SpawnCore("LC_DepartmentCore_CentralCommandTeam");
            Building_DepartmentCore welfare = SpawnCore("LC_DepartmentCore_WelfareTeam");
            Building_DepartmentCore disc = SpawnCore("LC_DepartmentCore_DisciplinaryTeam");

            int expectedMaxBullets = 10 + CodexIntelUtility.GetBonusMaxManagerBullets();
            if (ManagerBulletUtility.GetMaxBulletCharges(map) != expectedMaxBullets)
                throw new InvalidOperationException($"Expected {expectedMaxBullets} max bullet charges with Central+Welfare+Disciplinary (+Codex milestone bonus), got {ManagerBulletUtility.GetMaxBulletCharges(map)}.");

            GameComponent_LC lc = GameComponent_LC.Instance ?? throw new InvalidOperationException("GameComponent_LC.Instance is null.");
            lc.RefillManagerBullets(map);
            if (lc.GetManagerBulletCharges(map) != expectedMaxBullets)
                throw new InvalidOperationException($"RefillManagerBullets failed to restore charges to {expectedMaxBullets}.");

            if (central.GetGizmos().Count() < 6 || !welfare.GetGizmos().Any() || !disc.GetGizmos().Any())
                throw new InvalidOperationException("Department cores failed to expose Manager Bullet gizmos.");

            if (DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletFire") == null)
                throw new InvalidOperationException("LC_Sound_BulletFire SoundDef missing.");
            if (lc.GetManagerBulletRechargeProgress(map) < 0f || lc.GetManagerBulletRechargeProgress(map) > 1f)
                throw new InvalidOperationException("GetManagerBulletRechargeProgress out of [0,1] range.");
            foreach (EManagerBulletType bt in ManagerBulletUtility.OrderedBulletTypes)
            {
                if (string.IsNullOrEmpty(ManagerBulletUtility.GetBulletIconPath(bt)) || ManagerBulletUtility.GetBulletRadius(bt) <= 0f)
                    throw new InvalidOperationException($"Invalid metadata for bullet {bt}.");
            }
            IntVec3 emptyExecCell = FindFarthestSafeCell(map, minColonistDist: 10);
            int chargesBeforeInvalidExec = lc.GetManagerBulletCharges(map);
            if (ManagerBulletUtility.FireBulletAt(map, emptyExecCell, EManagerBulletType.Execution))
                throw new InvalidOperationException("Execution Bullet unexpectedly succeeded on empty cell.");
            if (lc.GetManagerBulletCharges(map) != chargesBeforeInvalidExec)
                throw new InvalidOperationException("Execution Bullet wasted a charge on invalid empty cell.");

            friendly = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
                canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
            GenSpawn.Spawn(friendly, FindSafePawnCell(map, map.Center), map);

            // HP Bullet (target Torso with AP=2 so random armor deflection or severed finger cannot flake the test)
            BodyPartRecord torsoPart = friendly.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def == BodyPartDefOf.Torso);
            friendly.TakeDamage(new DamageInfo(RimWorld.DamageDefOf.Cut, 12f, 2f, -1f, null, torsoPart));
            float injuredSummary = friendly.health.summaryHealth.SummaryHealthPercent;
            if (!ManagerBulletUtility.FireBulletAt(map, friendly.Position, EManagerBulletType.HpHeal))
                throw new InvalidOperationException("HP Bullet failed to fire.");
            if (friendly.health.summaryHealth.SummaryHealthPercent <= injuredSummary)
                throw new InvalidOperationException("HP Bullet did not heal friendly pawn injuries.");

            // SP Bullet
            Hediff mental = friendly.health.AddHediff(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
            mental.Severity = 0.75f;
            if (!ManagerBulletUtility.FireBulletAt(map, friendly.Position, EManagerBulletType.SpHeal))
                throw new InvalidOperationException("SP Bullet failed to fire.");
            if (mental.Severity >= 0.30f)
                throw new InvalidOperationException($"SP Bullet failed to reduce LC_MentalDamage (remaining={mental.Severity:F2}).");

            // Red Shield Bullet (absorbs matching LC_RedDamage, tracks remaining shield, shatters cleanly)
            if (!ManagerBulletUtility.FireBulletAt(map, friendly.Position, EManagerBulletType.RedShield))
                throw new InvalidOperationException("Red Shield Bullet failed to fire.");
            Hediff redShield = friendly.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("LC_BulletShield_Red", true))
                ?? throw new InvalidOperationException("Red Shield Bullet did not apply LC_BulletShield_Red.");
            var shieldComp = redShield.TryGetComp<LCAnomalyCore.Hediffs.HediffComp_BulletShield>()
                ?? throw new InvalidOperationException("LC_BulletShield_Red missing HediffComp_BulletShield.");
            float hpBeforeShieldHit = friendly.health.summaryHealth.SummaryHealthPercent;
            friendly.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 25f));
            if (Math.Abs(friendly.health.summaryHealth.SummaryHealthPercent - hpBeforeShieldHit) > 0.001f)
                throw new InvalidOperationException("Red Shield failed to absorb 25 LC_RedDamage.");
            if (Math.Abs(shieldComp.RemainingShield - 25f) > 0.5f)
                throw new InvalidOperationException($"Red Shield expected 25 remaining after absorbing 25, got {shieldComp.RemainingShield:F1}.");
            friendly.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 35f));
            if (friendly.health.hediffSet.HasHediff(redShield.def))
                throw new InvalidOperationException("Red Shield did not shatter after exceeding remaining shield capacity.");

            // Slow Bullet
            PawnKindDef slowKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("LCOrdeal_AmberDusk") ?? PawnKindDefOf.Mech_Scyther;
            slowTarget = PawnGenerator.GeneratePawn(slowKind, Faction.OfMechanoids);
            GenSpawn.Spawn(slowTarget, FindSafePawnCell(map, FindFarthestSafeCell(map)), map);
            float speedBeforeSlow = slowTarget.GetStatValue(RimWorld.StatDefOf.MoveSpeed);
            if (!ManagerBulletUtility.FireBulletAt(map, slowTarget.Position, EManagerBulletType.Slow))
                throw new InvalidOperationException("Slow Bullet failed to fire.");
            if (!slowTarget.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("LC_BulletSlow", true))
                || slowTarget.GetStatValue(RimWorld.StatDefOf.MoveSpeed) >= speedBeforeSlow * 0.65f)
            {
                throw new InvalidOperationException("Slow Bullet failed to halve hostile MoveSpeed.");
            }

            // Verify Red Shield visual draw & core heal pulse invocation are exception-free
            shieldComp.DrawShieldEffect(friendly.DrawPos, 0, 1);
            central.TryHeal();
            central.DrawExtraSelectionOverlays();

            // Execution Bullet (kills target and leaves a corpse rather than vanishing)
            if (friendly.Spawned) friendly.DeSpawn();
            execTarget = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
                canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
            IntVec3 execCell = FindFarthestSafeCell(map, minColonistDist: 8);
            GenSpawn.Spawn(execTarget, execCell, map);
            if (!ManagerBulletUtility.FireBulletAt(map, execTarget.Position, EManagerBulletType.Execution))
                throw new InvalidOperationException("Execution Bullet failed to fire.");
            if (!execTarget.Dead || !execCell.GetThingList(map).Any(t => t is Corpse c && c.InnerPawn == execTarget))
                throw new InvalidOperationException("Execution Bullet did not kill target with a corpse.");

            if (lc.GetManagerBulletCharges(map) != expectedMaxBullets - 5)
                throw new InvalidOperationException($"Expected {expectedMaxBullets - 5} remaining bullet charges after 5 shots, got {lc.GetManagerBulletCharges(map)}.");
            lc.RefillManagerBullets(map);
        }
        finally
        {
            foreach (Building_DepartmentCore core in cores)
            {
                if (core != null && !core.Destroyed) { core.DeSpawn(); core.Destroy(); }
            }
            foreach (Pawn p in new[] { friendly, slowTarget, execTarget })
            {
                if (p?.Corpse != null && !p.Corpse.Destroyed)
                {
                    if (p.Corpse.Spawned) p.Corpse.DeSpawn();
                    p.Corpse.Destroy();
                }
                if (p != null && !p.Destroyed) { if (p.Spawned) p.DeSpawn(); p.Destroy(); }
            }
        }

        // 2. Validate Dusk (3) & Midnight (6) Ordeals original textures, FourColorTargetExtension, and SoundDefs
        string[] duskMidnightVariants =
        {
            "AmberDusk", "CrimsonDusk", "GreenDusk",
            "AmberMidnight", "GreenMidnight",
            "VioletMidnightRed", "VioletMidnightWhite", "VioletMidnightBlack", "VioletMidnightPale"
        };
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail("LCOrdeal_AmberDusk") != null)
        {
            foreach (string variant in duskMidnightVariants)
            {
                PawnKindDef pk = DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_" + variant, true);
                if (pk.race.GetModExtension<LCAnomalyCore.ModExtensions.FourColorTargetExtension>() == null)
                    throw new InvalidOperationException($"Dusk/Midnight ordeal race {pk.race.defName} missing FourColorTargetExtension.");
                string texBase = "Things/Pawn/Ordeals/" + variant;
                if (ContentFinder<Texture2D>.Get(texBase + "_east", reportFailure: false) == null
                    || ContentFinder<Texture2D>.Get(texBase + "_south", reportFailure: false) == null)
                {
                    throw new InvalidOperationException($"Dusk/Midnight ordeal sprite textures missing for {texBase}.");
                }
            }

            string[] phase2OrdealSounds =
            {
                "LCOrdeal_Sound_BugDuskDig", "LCOrdeal_Sound_BugDuskSpawn", "LCOrdeal_Sound_BugDuskDead",
                "LCOrdeal_Sound_CircusDuskMove", "LCOrdeal_Sound_CircusDuskDead",
                "LCOrdeal_Sound_MachineDuskProduce", "LCOrdeal_Sound_MachineDuskDead",
                "LCOrdeal_Sound_BugMidnightDig", "LCOrdeal_Sound_BugMidnightSpawn", "LCOrdeal_Sound_BugMidnightDead",
                "LCOrdeal_Sound_MachineMidnightBeam", "LCOrdeal_Sound_MachineMidnightDead",
                "LCOrdeal_Sound_VioletMidnightPortal", "LCOrdeal_Sound_VioletMidnightRed",
                "LCOrdeal_Sound_VioletMidnightWhite", "LCOrdeal_Sound_VioletMidnightBlack",
                "LCOrdeal_Sound_VioletMidnightPale", "LCOrdeal_Sound_VioletMidnightDead"
            };
            foreach (string sndName in phase2OrdealSounds)
            {
                SoundDef sd = DefDatabase<SoundDef>.GetNamed(sndName, true);
                if (sd.subSounds == null || sd.subSounds.Count == 0)
                    throw new InvalidOperationException($"Phase 2 Ordeal SoundDef {sndName} has no subSounds.");
            }
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; ExpansionPhase2; manager_bullets_8types/shield_slow_exec/dusk_midnight_9art_18audio_fourcolor");
        ValidateOrdealAndDepartmentExpansionPhase3(map);
    }

    private static void ValidateOrdealAndDepartmentExpansionPhase3(Map map)
    {
        // 1. Validate all 6 Core Suppression Examinations & Nodes in LC-Anomaly-Story (if loaded)
        string[] examNames =
        {
            "LCStory_CoreSuppression_Malkuth",
            "LCStory_CoreSuppression_Yesod",
            "LCStory_CoreSuppression_Hod",
            "LCStory_CoreSuppression_Netzach",
            "LCStory_CoreSuppression_Gebura",
            "LCStory_CoreSuppression_Binah"
        };
        string[] nodeNames =
        {
            "LCStory_Node_SuppressMalkuth",
            "LCStory_Node_SuppressYesod",
            "LCStory_Node_SuppressHod",
            "LCStory_Node_SuppressNetzach",
            "LCStory_Node_SuppressGebura",
            "LCStory_Node_SuppressBinah"
        };
        Type examDefType = GenTypes.GetTypeInAnyAssembly("LCAnomalyStory.Defs.CompanyExaminationDef");
        Type devDefType = GenTypes.GetTypeInAnyAssembly("LCAnomalyStory.Defs.CompanyDevelopmentDef");
        if (examDefType != null && devDefType != null)
        {
            MethodInfo getExam = typeof(DefDatabase<>).MakeGenericType(examDefType).GetMethod("GetNamed", new[] { typeof(string), typeof(bool) });
            MethodInfo getNode = typeof(DefDatabase<>).MakeGenericType(devDefType).GetMethod("GetNamed", new[] { typeof(string), typeof(bool) });
            foreach (string en in examNames) getExam.Invoke(null, new object[] { en, true });
            foreach (string nn in nodeNames) getNode.Invoke(null, new object[] { nn, true });
        }

        // 2. Validate Hod debuff HediffDef & all 6 Core Suppression permanent mastery hooks in GameComponent_LC
        HediffDef hodDebuff = DefDatabase<HediffDef>.GetNamed("LC_CoreSuppression_HodDebuff", true);
        if (hodDebuff.stages == null || hodDebuff.stages.Count == 0)
            throw new InvalidOperationException("LC_CoreSuppression_HodDebuff missing stat stage.");

        GameComponent_LC lc = GameComponent_LC.Instance ?? throw new InvalidOperationException("GameComponent_LC.Instance is null.");
        lc.CompleteCoreSuppression(GameComponent_LC.CoreSuppression_Malkuth);
        lc.CompleteCoreSuppression(GameComponent_LC.CoreSuppression_Yesod);
        lc.CompleteCoreSuppression(GameComponent_LC.CoreSuppression_Hod);
        lc.CompleteCoreSuppression(GameComponent_LC.CoreSuppression_Netzach);
        lc.CompleteCoreSuppression(GameComponent_LC.CoreSuppression_Gebura);
        lc.CompleteCoreSuppression(GameComponent_LC.CoreSuppression_Binah);

        if (DepartmentCoreUtility.GetBonusPeBoxChance(null) < 0.249f)
            throw new InvalidOperationException("Yesod Core Suppression failed to grant +25% bonus PE-Box chance.");
        if (DepartmentCoreUtility.GetExpGainMultiplier(null) < 1.499f)
            throw new InvalidOperationException("Hod Core Suppression failed to grant +50% four-attribute XP gain.");
        if (Math.Abs(DepartmentCoreUtility.GetEffectiveHealFuelCost(10f) - 7f) > 0.01f
            || DepartmentCoreUtility.GetEffectiveHealMentalAmount(0f) < 0.149f)
            throw new InvalidOperationException("Netzach Core Suppression failed to apply -30% fuel and built-in SP recovery.");
        if (DepartmentCoreUtility.GetEgoExtractionCapBonus(map) < 1)
            throw new InvalidOperationException("Gebura Core Suppression failed to grant permanent +1 EGO extraction cap.");
        if (DepartmentCoreUtility.ApplyEgoExtractionCost(20, map) != 16)
            throw new InvalidOperationException($"Binah Core Suppression expected 20 -> 16 EGO cost, got {DepartmentCoreUtility.ApplyEgoExtractionCost(20, map)}.");

        // 3. Validate all 5 White Ordeal entities (4 Fixers + The Claw) art, FourColorTargetExtension, and SoundDefs
        string[] whiteVariants =
        {
            "WhiteFixerRed", "WhiteFixerWhite", "WhiteFixerBlack", "WhiteFixerPale", "WhiteClaw"
        };
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail("LCOrdeal_WhiteClaw") != null)
        {
            foreach (string variant in whiteVariants)
            {
                PawnKindDef pk = DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_" + variant, true);
                var fc = pk.race.GetModExtension<LCAnomalyCore.ModExtensions.FourColorTargetExtension>()
                    ?? throw new InvalidOperationException($"White Ordeal race {pk.race.defName} missing FourColorTargetExtension.");
                if (variant == "WhiteClaw" && Math.Abs(fc.paleResistance - 0.4f) > 0.001f)
                    throw new InvalidOperationException($"WhiteClaw expected 0.4 Pale resistance, got {fc.paleResistance}.");
                string texBase = "Things/Pawn/Ordeals/" + variant;
                if (ContentFinder<Texture2D>.Get(texBase + "_east", reportFailure: false) == null
                    || ContentFinder<Texture2D>.Get(texBase + "_south", reportFailure: false) == null)
                {
                    throw new InvalidOperationException($"White Ordeal sprite textures missing for {texBase}.");
                }
            }

            string[] whiteSounds =
            {
                "LCOrdeal_Sound_WhiteStart", "LCOrdeal_Sound_WhiteEnd",
                "LCOrdeal_Sound_FixerRedAtk", "LCOrdeal_Sound_FixerRedDead",
                "LCOrdeal_Sound_FixerWhiteAtk", "LCOrdeal_Sound_FixerWhiteReflect", "LCOrdeal_Sound_FixerWhiteDead",
                "LCOrdeal_Sound_FixerBlackAtk", "LCOrdeal_Sound_FixerBlackDead",
                "LCOrdeal_Sound_FixerPaleTeleport", "LCOrdeal_Sound_FixerPaleAtk", "LCOrdeal_Sound_FixerPaleDead",
                "LCOrdeal_Sound_ClawNormal", "LCOrdeal_Sound_ClawOrange", "LCOrdeal_Sound_ClawBlue",
                "LCOrdeal_Sound_ClawGreen", "LCOrdeal_Sound_ClawDead"
            };
            foreach (string sndName in whiteSounds)
            {
                SoundDef sd = DefDatabase<SoundDef>.GetNamed(sndName, true);
                if (sd.subSounds == null || sd.subSounds.Count == 0)
                    throw new InvalidOperationException($"White Ordeal SoundDef {sndName} has no subSounds.");
            }
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; ExpansionPhase3; 6_core_suppressions/mastery_hooks/white_ordeals_5art_17audio_fourcolor");
        ValidateOrdealAnimationsAndVfx(map);
    }

    private static void ValidateOrdealAnimationsAndVfx(Map map)
    {
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail("LCOrdeal_WhiteClaw") == null)
        {
            return;
        }

        PawnRenderTreeDef treeDef = DefDatabase<PawnRenderTreeDef>.GetNamed("LCOrdeal_Animated", true);
        Type compAnimType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Render.CompOrdealAnimator")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Render.CompOrdealAnimator type.");
        Type compDawnType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Comps.CompDawnOrdeal")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Comps.CompDawnOrdeal type.");
        Type compCrimsonType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Comps.CompCrimsonOrdeal")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Comps.CompCrimsonOrdeal type.");
        Type compAmberType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Comps.CompAmberOrdeal")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Comps.CompAmberOrdeal type.");
        Type compGreenType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Comps.CompGreenOrdeal")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Comps.CompGreenOrdeal type.");
        Type compVioletType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Comps.CompVioletOrdeal")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Comps.CompVioletOrdeal type.");
        Type compWiType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Comps.CompWhiteAndIndigoOrdeal")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Comps.CompWhiteAndIndigoOrdeal type.");
        Type vfxUtilType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Vfx.OrdealVfxUtility")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Vfx.OrdealVfxUtility type.");
        Type vfxCompType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Vfx.MapComponent_OrdealVfx")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Vfx.MapComponent_OrdealVfx type.");

        // 1. Validate ALL 22/22 Ordeals: articulated Spine RenderTrees, part PNGs, Comps, Categories, and FourColorWeapon/Target
        var articulatedSpecs = new[]
        {
            // 4 Dawn (TETH)
            new { Variant = "AmberDawn",   Tree = "LCOrdeal_Tree_AmberDawn",   Comp = compDawnType,    Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.TETH,  Cat = "TETH",  Parts = new[] { "Body", "UpperJaw", "LowerJaw", "LegFront", "LegMid", "LegBack", "PlatesFront", "PlatesBack" } },
            new { Variant = "CrimsonDawn", Tree = "LCOrdeal_Tree_CrimsonDawn", Comp = compDawnType,    Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.TETH,  Cat = "TETH",  Parts = new[] { "Body", "Head", "Tongue", "HatCheckered", "HatBlack", "LeftArm", "RightArm", "LeftLeg", "RightLeg" } },
            new { Variant = "GreenDawn",   Tree = "LCOrdeal_Tree_GreenDawn",   Comp = compDawnType,    Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.TETH,  Cat = "TETH",  Parts = new[] { "Pelvis", "Torso", "ClothBack", "ClothFront", "BackLegUpper", "BackLegLower", "FrontLegUpper", "FrontLegLower", "Head_Green", "Head_Red", "WeaponArm", "WeaponSpear" } },
            new { Variant = "VioletDawn",  Tree = "LCOrdeal_Tree_VioletDawn",  Comp = compDawnType,    Dmg = LCAnomalyCore.Damage.FourColorDamageType.Black, Risk = LCAnomalyCore.Damage.LCRiskLevel.TETH,  Cat = "TETH",  Parts = new[] { "Body", "Tail", "CrawlLeg", "LightCore", "Tendril1", "Tendril2", "Tendril3" } },
            // 2 Crimson High (HE / WAW)
            new { Variant = "CrimsonNoon", Tree = "LCOrdeal_Tree_CrimsonNoon", Comp = compCrimsonType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.HE,    Cat = "HE",    Parts = new[] { "Body", "BackLeg1", "BackLeg2", "FrontLeg1", "FrontLeg2", "BackPincerArm", "BackPincerHead", "FrontPincerArm", "FrontPincerHead", "TailBase", "TailHead" } },
            new { Variant = "CrimsonDusk", Tree = "LCOrdeal_Tree_CrimsonDusk", Comp = compCrimsonType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "BackLeg", "FrontLeg", "ClownHead", "WeaponRight", "WeaponBack", "WeaponTop", "HammerArm", "HammerHead" } },
            // 2 Amber High (WAW / ALEPH)
            new { Variant = "AmberDusk",     Tree = "LCOrdeal_Tree_AmberDusk",     Comp = compAmberType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red, Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "UpperJaw", "LowerJaw", "PlatesBack", "PlatesMid", "PlatesFront", "LegFront", "LegMid", "LegBack" } },
            new { Variant = "AmberMidnight", Tree = "LCOrdeal_Tree_AmberMidnight", Comp = compAmberType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red, Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "Body", "InnerJawLeft", "InnerJawRight", "LeftWing", "RightWing" } },
            // 3 Green High (HE / WAW / ALEPH)
            new { Variant = "GreenNoon",     Tree = "LCOrdeal_Tree_GreenNoon",     Comp = compGreenType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.HE,    Cat = "HE",    Parts = new[] { "Body", "BackLeg", "FrontLeg", "Head", "SawArm", "SawBlade", "GunArm" } },
            new { Variant = "GreenDusk",     Tree = "LCOrdeal_Tree_GreenDusk",     Comp = compGreenType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "FurnaceBack", "Gears", "ExhaustPipes", "ChargeGauge", "BlastGate" } },
            new { Variant = "GreenMidnight", Tree = "LCOrdeal_Tree_GreenMidnight", Comp = compGreenType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Black, Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "BaseCore", "LeftShell", "RightShell", "Emitter", "EmitterGlow" } },
            // 5 Violet High (HE / 4x ALEPH Shrines)
            new { Variant = "VioletNoon",          Tree = "LCOrdeal_Tree_VioletNoon",          Comp = compVioletType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Black, Risk = LCAnomalyCore.Damage.LCRiskLevel.HE,    Cat = "HE",    Parts = new[] { "Monolith", "RunesGlow", "Tentacle1", "Tentacle2", "Tentacle3", "Tentacle4", "Tentacle5", "Tentacle6" } },
            new { Variant = "VioletMidnightRed",   Tree = "LCOrdeal_Tree_VioletMidnightRed",   Comp = compVioletType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "ShrineBase", "RuneAura" } },
            new { Variant = "VioletMidnightWhite", Tree = "LCOrdeal_Tree_VioletMidnightWhite", Comp = compVioletType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.White, Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "ShrineBase", "RuneAura" } },
            new { Variant = "VioletMidnightBlack", Tree = "LCOrdeal_Tree_VioletMidnightBlack", Comp = compVioletType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Black, Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "ShrineBase", "RuneAura" } },
            new { Variant = "VioletMidnightPale",  Tree = "LCOrdeal_Tree_VioletMidnightPale",  Comp = compVioletType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Pale,  Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "ShrineBase", "RuneAura" } },
            // 6 Indigo Sweeper (HE), 4 Color Fixers (WAW), and The Claw (ALEPH)
            new { Variant = "IndigoNoon",      Tree = "LCOrdeal_Tree_IndigoNoon",      Comp = compWiType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Black, Risk = LCAnomalyCore.Damage.LCRiskLevel.HE,    Cat = "HE",    Parts = new[] { "Body", "Head", "BackLeg", "FrontLeg", "BackArm", "FrontArm" } },
            new { Variant = "WhiteFixerRed",   Tree = "LCOrdeal_Tree_WhiteFixerRed",   Comp = compWiType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "Head", "BackLeg", "FrontLeg", "BackArm", "FrontArm", "Weapon" } },
            new { Variant = "WhiteFixerWhite", Tree = "LCOrdeal_Tree_WhiteFixerWhite", Comp = compWiType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.White, Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "Head", "Halo", "BackLeg", "FrontLeg", "BackArm", "FrontArm", "Cannon" } },
            new { Variant = "WhiteFixerBlack", Tree = "LCOrdeal_Tree_WhiteFixerBlack", Comp = compWiType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Black, Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "Head", "BackLeg", "FrontLeg", "BackArm", "FrontArm", "Hammer" } },
            new { Variant = "WhiteFixerPale",  Tree = "LCOrdeal_Tree_WhiteFixerPale",  Comp = compWiType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Pale,  Risk = LCAnomalyCore.Damage.LCRiskLevel.WAW,   Cat = "WAW",   Parts = new[] { "Body", "Head", "BackLeg", "FrontLeg", "BackArm", "Pistol", "FrontArm", "Briefcase" } },
            new { Variant = "WhiteClaw",       Tree = "LCOrdeal_Tree_WhiteClaw",       Comp = compWiType, Dmg = LCAnomalyCore.Damage.FourColorDamageType.Red,   Risk = LCAnomalyCore.Damage.LCRiskLevel.ALEPH, Cat = "ALEPH", Parts = new[] { "Torso", "Head", "SerumGreen", "BackLeg", "FrontLeg", "BackArm", "ClawUpperArm", "ClawForearm", "ClawHand" } }
        };
        foreach (var sp in articulatedSpecs)
        {
            PawnKindDef pk = DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_" + sp.Variant, true);
            PawnRenderTreeDef expectedTree = DefDatabase<PawnRenderTreeDef>.GetNamed(sp.Tree, true);
            if (pk.race.race.renderTree != expectedTree)
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} expected articulated renderTree {sp.Tree}, got {pk.race.race.renderTree?.defName ?? "<null>"}.");
            if (pk.race.comps == null || !pk.race.comps.Any(c => c.compClass == sp.Comp))
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} missing {sp.Comp.Name}.");

            var fcTarget = pk.race.GetModExtension<LCAnomalyCore.ModExtensions.FourColorTargetExtension>()
                ?? throw new InvalidOperationException($"Ordeal race {pk.race.defName} missing FourColorTargetExtension.");
            if (fcTarget.targetKind != LCAnomalyCore.Damage.FourColorTargetKind.Abnormality)
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} expected targetKind=Abnormality.");

            var fcWeapon = pk.race.GetModExtension<LCAnomalyCore.ModExtensions.FourColorWeaponExtension>()
                ?? throw new InvalidOperationException($"Ordeal race {pk.race.defName} missing FourColorWeaponExtension.");
            if (fcWeapon.damageType != sp.Dmg || fcWeapon.riskLevel != sp.Risk)
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} expected FourColorWeaponExtension {sp.Dmg}/{sp.Risk}.");

            var catExt = pk.race.GetModExtension<LCAnomalyCore.ModExtensions.ModExtension_AbnormalityCategory>()
                ?? throw new InvalidOperationException($"Ordeal race {pk.race.defName} missing ModExtension_AbnormalityCategory.");
            if (catExt.abnormalityCategoryDef?.defName != sp.Cat)
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} expected {sp.Cat} category.");

            DamageInfo probe = new DamageInfo(RimWorld.DamageDefOf.Cut, 15f, 0f, -1f, null, null, pk.race);
            LCAnomalyCore.Util.DamageUtils.ConvertWeaponDamage(ref probe);
            DamageDef expectedDef = sp.Dmg == LCAnomalyCore.Damage.FourColorDamageType.Black ? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage
                : sp.Dmg == LCAnomalyCore.Damage.FourColorDamageType.White ? LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage
                : sp.Dmg == LCAnomalyCore.Damage.FourColorDamageType.Pale ? LCAnomalyCore.Defs.DamageDefOf.LC_PaleDamage
                : LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            if (probe.Def != expectedDef)
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} melee conversion expected {expectedDef.defName}, got {probe.Def?.defName ?? "<null>"}.");

            foreach (string part in sp.Parts)
            {
                string pPath = $"Things/Pawn/Ordeals/{sp.Variant}/Parts/{part}";
                if (ContentFinder<Texture2D>.Get(pPath, reportFailure: false) == null
                    || ContentFinder<Texture2D>.Get(pPath + "_L", reportFailure: false) == null)
                {
                    throw new InvalidOperationException($"Missing articulated Ordeal part texture: {pPath}(_L)");
                }
            }
        }

        string[] articulatedAnims =
        {
            "LCOrdeal_AmberDawn_Idle", "LCOrdeal_AmberDawn_Walk", "LCOrdeal_AmberDawn_Attack", "LCOrdeal_AmberDawn_DigIn", "LCOrdeal_AmberDawn_DigOut",
            "LCOrdeal_CrimsonDawn_Idle", "LCOrdeal_CrimsonDawn_Walk", "LCOrdeal_CrimsonDawn_Attack", "LCOrdeal_CrimsonDawn_Tamper", "LCOrdeal_CrimsonDawn_Teleport",
            "LCOrdeal_GreenDawn_Idle", "LCOrdeal_GreenDawn_Walk", "LCOrdeal_GreenDawn_Attack1", "LCOrdeal_GreenDawn_Attack2", "LCOrdeal_GreenDawn_Execution",
            "LCOrdeal_VioletDawn_Idle", "LCOrdeal_VioletDawn_Walk", "LCOrdeal_VioletDawn_Attack", "LCOrdeal_VioletDawn_Charge",
            "LCOrdeal_CrimsonNoon_Idle", "LCOrdeal_CrimsonNoon_Walk", "LCOrdeal_CrimsonNoon_Attack1", "LCOrdeal_CrimsonNoon_Attack2", "LCOrdeal_CrimsonNoon_Attack3",
            "LCOrdeal_CrimsonDusk_Idle", "LCOrdeal_CrimsonDusk_Walk", "LCOrdeal_CrimsonDusk_Roll", "LCOrdeal_CrimsonDusk_Attack1", "LCOrdeal_CrimsonDusk_Attack2",
            "LCOrdeal_AmberDusk_Idle", "LCOrdeal_AmberDusk_Walk", "LCOrdeal_AmberDusk_Attack", "LCOrdeal_AmberDusk_Spawn", "LCOrdeal_AmberDusk_DigIn", "LCOrdeal_AmberDusk_DigOut",
            "LCOrdeal_AmberMidnight_Idle", "LCOrdeal_AmberMidnight_Roar", "LCOrdeal_AmberMidnight_DigIn", "LCOrdeal_AmberMidnight_DigOut",
            "LCOrdeal_GreenNoon_Idle", "LCOrdeal_GreenNoon_Walk", "LCOrdeal_GreenNoon_Attack", "LCOrdeal_GreenNoon_Cooldown",
            "LCOrdeal_GreenDusk_Idle", "LCOrdeal_GreenDusk_Create",
            "LCOrdeal_GreenMidnight_Idle", "LCOrdeal_GreenMidnight_Charge", "LCOrdeal_GreenMidnight_Fire",
            "LCOrdeal_VioletNoon_Idle", "LCOrdeal_VioletNoon_Impact", "LCOrdeal_VioletNoon_Cast",
            "LCOrdeal_VioletShrine_Idle", "LCOrdeal_VioletShrine_Cast",
            "LCOrdeal_IndigoNoon_Idle", "LCOrdeal_IndigoNoon_Walk", "LCOrdeal_IndigoNoon_Attack", "LCOrdeal_IndigoNoon_Eat",
            "LCOrdeal_FixerRed_Idle", "LCOrdeal_FixerRed_Walk", "LCOrdeal_FixerRed_Attack", "LCOrdeal_FixerRed_Special",
            "LCOrdeal_FixerWhite_Idle", "LCOrdeal_FixerWhite_Walk", "LCOrdeal_FixerWhite_Attack", "LCOrdeal_FixerWhite_Special",
            "LCOrdeal_FixerBlack_Idle", "LCOrdeal_FixerBlack_Walk", "LCOrdeal_FixerBlack_Attack",
            "LCOrdeal_FixerPale_Idle", "LCOrdeal_FixerPale_Walk", "LCOrdeal_FixerPale_Attack",
            "LCOrdeal_WhiteClaw_Idle", "LCOrdeal_WhiteClaw_Walk", "LCOrdeal_WhiteClaw_Attack", "LCOrdeal_WhiteClaw_Serum", "LCOrdeal_WhiteClaw_Groggy"
        };
        foreach (string animName in articulatedAnims)
        {
            if (DefDatabase<AnimationDef>.GetNamed(animName, true) == null)
                throw new InvalidOperationException($"Missing articulated Ordeal AnimationDef {animName}.");
        }
        if (treeDef == null || compAnimType == null)
            throw new InvalidOperationException("Fallback ordeal render tree or animator comp missing.");

        string[] vfxTextures =
        {
            "FX/Ordeals/LaserBeam", "FX/Ordeals/LaserCore", "FX/Ordeals/LaserHit", "FX/Ordeals/SparkMidnight",
            "FX/Ordeals/BossPortal", "FX/Ordeals/HollowRing", "FX/Ordeals/SoftGlow", "FX/Ordeals/CraterDecal",
            "FX/Ordeals/SlashArc", "FX/Ordeals/TargetMark",
            "FX/Ordeals/TentacleRed", "FX/Ordeals/TentacleWhite", "FX/Ordeals/TentacleBlack", "FX/Ordeals/PaleEye",
            "FX/Ordeals/CircusTent"
        };
        foreach (string vfxTex in vfxTextures)
        {
            if (ContentFinder<Texture2D>.Get(vfxTex, reportFailure: false) == null)
                throw new InvalidOperationException($"Missing ordeal VFX texture: {vfxTex}");
        }

        Pawn testClaw = null;
        Pawn testTower = null;
        var testDawns = new List<Pawn>();
        try
        {
            IntVec3 cell1 = FindSafePawnCell(map, FindFarthestSafeCell(map));
            IntVec3 cell2 = FindSafePawnCell(map, cell1);
            testClaw = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_WhiteClaw", true), Faction.OfMechanoids);
            testTower = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_GreenMidnight", true), Faction.OfMechanoids);
            GenSpawn.Spawn(testClaw, cell1, map);
            GenSpawn.Spawn(testTower, cell2, map);

            // Live articulated render + animation state verification for all 4 Dawn Ordeals
            foreach (string dKind in new[] { "LCOrdeal_AmberDawn", "LCOrdeal_CrimsonDawn", "LCOrdeal_GreenDawn", "LCOrdeal_VioletDawn" })
            {
                Pawn dp = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(dKind, true), Faction.OfMechanoids);
                GenSpawn.Spawn(dp, FindSafePawnCell(map, cell2), map);
                testDawns.Add(dp);
                ThingComp dc = dp.AllComps.FirstOrDefault(c => c.GetType() == compDawnType)
                    ?? throw new InvalidOperationException($"Spawned {dKind} missing CompDawnOrdeal.");
                if (dKind == "LCOrdeal_GreenDawn")
                    compDawnType.GetMethod("BeginGreenExecution").Invoke(dc, new object[] { dp.Position });
                else if (dKind == "LCOrdeal_VioletDawn")
                    compDawnType.GetMethod("SetVioletChargeProgress").Invoke(dc, new object[] { 0.85f });
                else
                    compDawnType.GetMethod("NotifyMeleeAttack").Invoke(dc, new object[] { null });
                dc.CompTick();
                dp.Drawer.renderer.EnsureGraphicsInitialized();
                dp.Drawer.renderer.RenderPawnAt(dp.DrawPos);
                if (dKind == "LCOrdeal_GreenDawn" && dp.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_GreenDawn_Execution")
                    throw new InvalidOperationException($"GreenDawn expected LCOrdeal_GreenDawn_Execution, got {dp.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
                if (dKind == "LCOrdeal_VioletDawn" && dp.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_VioletDawn_Charge")
                    throw new InvalidOperationException($"VioletDawn expected LCOrdeal_VioletDawn_Charge, got {dp.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
            }

            // Live articulated render + state verification across Crimson, Amber, Green, and Violet high-tier Ordeals
            foreach (string cKind in new[] { "LCOrdeal_CrimsonNoon", "LCOrdeal_CrimsonDusk" })
            {
                Pawn cp = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(cKind, true), Faction.OfMechanoids);
                GenSpawn.Spawn(cp, FindSafePawnCell(map, cell2), map);
                testDawns.Add(cp);
                ThingComp cc = cp.AllComps.FirstOrDefault(c => c.GetType() == compCrimsonType)
                    ?? throw new InvalidOperationException($"Spawned {cKind} missing CompCrimsonOrdeal.");
                if (cKind == "LCOrdeal_CrimsonNoon")
                {
                    compCrimsonType.GetMethod("NotifyMeleeAttack").Invoke(cc, new object[] { null });
                    cc.CompTick();
                    cp.Drawer.renderer.EnsureGraphicsInitialized();
                    cp.Drawer.renderer.RenderPawnAt(cp.DrawPos);
                    if (cp.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_CrimsonNoon_Attack1")
                        throw new InvalidOperationException($"CrimsonNoon expected LCOrdeal_CrimsonNoon_Attack1, got {cp.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
                }
                else
                {
                    compCrimsonType.GetMethod("BeginRollCharge").Invoke(cc, new object[] { FindSafePawnCell(map, cell1) });
                    cc.CompTick();
                    cp.Drawer.renderer.EnsureGraphicsInitialized();
                    cp.Drawer.renderer.RenderPawnAt(cp.DrawPos);
                    if (cp.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_CrimsonDusk_Roll")
                        throw new InvalidOperationException($"CrimsonDusk expected LCOrdeal_CrimsonDusk_Roll, got {cp.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");
                }
            }

            // Live verification: Amber Midnight (split-jaw Roar), Green Dusk (Create), Green Midnight (360 Fire), Violet Noon (Cast), Violet Midnight Red (Cast)
            Pawn testAmberMid = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_AmberMidnight", true), Faction.OfMechanoids);
            GenSpawn.Spawn(testAmberMid, FindSafePawnCell(map, cell2), map);
            testDawns.Add(testAmberMid);
            ThingComp amc = testAmberMid.AllComps.FirstOrDefault(c => c.GetType() == compAmberType) ?? throw new InvalidOperationException("Missing CompAmberOrdeal.");
            compAmberType.GetMethod("TriggerSpawnRoar").Invoke(amc, null);
            amc.CompTick();
            testAmberMid.Drawer.renderer.EnsureGraphicsInitialized();
            testAmberMid.Drawer.renderer.RenderPawnAt(testAmberMid.DrawPos);

            Pawn testGreenDusk = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_GreenDusk", true), Faction.OfMechanoids);
            GenSpawn.Spawn(testGreenDusk, FindSafePawnCell(map, cell2), map);
            testDawns.Add(testGreenDusk);
            ThingComp gdc = testGreenDusk.AllComps.FirstOrDefault(c => c.GetType() == compGreenType) ?? throw new InvalidOperationException("Missing CompGreenOrdeal on GreenDusk.");
            compGreenType.GetMethod("TriggerFactoryCreate").Invoke(gdc, null);
            gdc.CompTick();
            testGreenDusk.Drawer.renderer.EnsureGraphicsInitialized();
            testGreenDusk.Drawer.renderer.RenderPawnAt(testGreenDusk.DrawPos);
            if (testGreenDusk.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_GreenDusk_Create")
                throw new InvalidOperationException($"GreenDusk expected LCOrdeal_GreenDusk_Create, got {testGreenDusk.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");

            ThingComp gmc = testTower.AllComps.FirstOrDefault(c => c.GetType() == compGreenType) ?? throw new InvalidOperationException("Missing CompGreenOrdeal on GreenMidnight.");
            compGreenType.GetMethod("NotifyMidnightFire").Invoke(gmc, new object[] { 135f, 30 });
            gmc.CompTick();
            testTower.Drawer.renderer.EnsureGraphicsInitialized();
            testTower.Drawer.renderer.RenderPawnAt(testTower.DrawPos);
            if (testTower.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_GreenMidnight_Fire")
                throw new InvalidOperationException($"GreenMidnight expected LCOrdeal_GreenMidnight_Fire, got {testTower.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");

            foreach (string vKind in new[] { "LCOrdeal_VioletNoon", "LCOrdeal_VioletMidnightRed" })
            {
                Pawn vp = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(vKind, true), Faction.OfMechanoids);
                GenSpawn.Spawn(vp, FindSafePawnCell(map, cell2), map);
                testDawns.Add(vp);
                ThingComp vc = vp.AllComps.FirstOrDefault(c => c.GetType() == compVioletType) ?? throw new InvalidOperationException($"Missing CompVioletOrdeal on {vKind}.");
                compVioletType.GetMethod("TriggerCast").Invoke(vc, new object[] { 50 });
                vc.CompTick();
                vp.Drawer.renderer.EnsureGraphicsInitialized();
                vp.Drawer.renderer.RenderPawnAt(vp.DrawPos);
            }

            // Live verification for Indigo Sweeper (Eat), White Fixer Red (Special), and The Claw (Serum -> Attack)
            Pawn testSweeper = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_IndigoNoon", true), Faction.OfMechanoids);
            GenSpawn.Spawn(testSweeper, FindSafePawnCell(map, cell2), map);
            testDawns.Add(testSweeper);
            ThingComp swc = testSweeper.AllComps.FirstOrDefault(c => c.GetType() == compWiType) ?? throw new InvalidOperationException("Missing CompWhiteAndIndigoOrdeal on IndigoNoon.");
            compWiType.GetMethod("TriggerEat").Invoke(swc, new object[] { 48 });
            swc.CompTick();
            testSweeper.Drawer.renderer.EnsureGraphicsInitialized();
            testSweeper.Drawer.renderer.RenderPawnAt(testSweeper.DrawPos);
            if (testSweeper.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_IndigoNoon_Eat")
                throw new InvalidOperationException($"IndigoNoon expected LCOrdeal_IndigoNoon_Eat, got {testSweeper.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");

            Pawn testRedFixer = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_WhiteFixerRed", true), Faction.OfMechanoids);
            GenSpawn.Spawn(testRedFixer, FindSafePawnCell(map, cell2), map);
            testDawns.Add(testRedFixer);
            ThingComp rfc = testRedFixer.AllComps.FirstOrDefault(c => c.GetType() == compWiType) ?? throw new InvalidOperationException("Missing CompWhiteAndIndigoOrdeal on WhiteFixerRed.");
            compWiType.GetMethod("TriggerSpecial").Invoke(rfc, new object[] { 46, (IntVec3?)cell1 });
            rfc.CompTick();
            testRedFixer.Drawer.renderer.EnsureGraphicsInitialized();
            testRedFixer.Drawer.renderer.RenderPawnAt(testRedFixer.DrawPos);
            if (testRedFixer.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_FixerRed_Special")
                throw new InvalidOperationException($"WhiteFixerRed expected LCOrdeal_FixerRed_Special, got {testRedFixer.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");

            ThingComp clawComp = testClaw.AllComps.FirstOrDefault(c => c.GetType() == compWiType) ?? throw new InvalidOperationException("Missing CompWhiteAndIndigoOrdeal on WhiteClaw.");
            compWiType.GetMethod("TriggerSpecial").Invoke(clawComp, new object[] { 54, (IntVec3?)cell2 });
            clawComp.CompTick();
            testClaw.Drawer.renderer.EnsureGraphicsInitialized();
            testClaw.Drawer.renderer.RenderPawnAt(testClaw.DrawPos);
            if (testClaw.Drawer.renderer.CurAnimation?.defName != "LCOrdeal_WhiteClaw_Serum")
                throw new InvalidOperationException($"WhiteClaw expected LCOrdeal_WhiteClaw_Serum, got {testClaw.Drawer.renderer.CurAnimation?.defName ?? "<null>"}.");

            vfxUtilType.GetMethod("SpawnBeam", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testTower.DrawPos, 45f, 24f, 1.8f, Color.green, 30, true });
            vfxUtilType.GetMethod("SpawnShockwave", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 0.5f, 4f, Color.cyan, 30, 90f });
            vfxUtilType.GetMethod("SpawnPortal", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 3f, Color.magenta, 30, 160f });
            vfxUtilType.GetMethod("SpawnStrikeSprite", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, "FX/Ordeals/PaleEye", 2f, 4f, Color.white, 30, 0.8f, 0f, 0f, false });
            vfxUtilType.GetMethod("SpawnPawnAura", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw, "FX/Ordeals/TargetMark", 1.8f, Color.blue, 30, 90f, 0.25f, true });
            vfxUtilType.GetMethod("SpawnFogLine", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, testTower.DrawPos, 2f, 30 });
            vfxUtilType.GetMethod("SpawnFanSweep", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 0f, 240f, 6f, 2f, Color.red, 30 });
            vfxUtilType.GetMethod("SpawnCircusTent", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 2.8f, 40 });

            MapComponent vfxComp = map.GetComponent(vfxCompType)
                ?? throw new InvalidOperationException("MapComponent_OrdealVfx not attached to map.");
            vfxComp.MapComponentUpdate();
            int activeCount = (int)vfxCompType.GetProperty("ActiveCount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vfxComp);
            if (activeCount < 8)
                throw new InvalidOperationException($"Expected >= 8 active ordeal VFX instances, got {activeCount}.");
        }
        finally
        {
            foreach (Pawn dp in testDawns) { if (dp != null && !dp.Destroyed) { if (dp.Spawned) dp.DeSpawn(); dp.Destroy(); } }
            if (testClaw != null && !testClaw.Destroyed) { if (testClaw.Spawned) testClaw.DeSpawn(); testClaw.Destroy(); }
            if (testTower != null && !testTower.Destroyed) { if (testTower.Spawned) testTower.DeSpawn(); testTower.Destroy(); }
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; OrdealAnimAndVfx; all_22_articulated_ordeals_76anims_4color_melee/15_vfx_textures/live_vfx_draw");
    }

    private static void ValidatePhase3AfterLoad(Map map)
    {
        bool foundSavedBackwardClock = false;
        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "BackwardClock"
                && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430311)
            {
                CompAbnormalityTool tool = plat.HeldToolComp;
                int charge = (int)(tool?.GetType().GetProperty("ChargeLevel", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tool) ?? -1);
                if (charge != 3)
                {
                    throw new InvalidOperationException($"Saved BackwardClock expected ChargeLevel=3 after reload, got {charge}.");
                }

                plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);
                foundSavedBackwardClock = true;
                break;
            }
        }

        if (!foundSavedBackwardClock)
            throw new InvalidOperationException("Saved BackwardClock containment terminal missing after reload.");

        if (GameComponent_LC.Instance?.HasCoreSuppression(GameComponent_LC.CoreSuppression_Malkuth) != true
            || GameComponent_LC.Instance?.HasCoreSuppression(GameComponent_LC.CoreSuppression_Binah) != true)
        {
            throw new InvalidOperationException("Core Suppression mastery flags did not survive save/load.");
        }
    }

    private static void ValidatePhase4(Map map, object abnSettings)
    {
        (string name, string cat, FourColorDamageType dmgType, LCRiskLevel risk, float rRed, float rWhite, float rBlack, float rPale)[] phase4 =
        {
            ("Ppodae", "TETH", FourColorDamageType.Red, LCRiskLevel.TETH, 0.8f, 1.5f, 0.8f, 2.0f),
            ("WarmHeartedWoodsman", "HE", FourColorDamageType.Red, LCRiskLevel.HE, 0.8f, 1.2f, 0.8f, 1.5f),
            ("FragmentOfTheUniverse", "TETH", FourColorDamageType.Black, LCRiskLevel.TETH, 1.0f, 1.2f, 0.6f, 2.0f),
            ("ScarecrowSearchingForWisdom", "HE", FourColorDamageType.Black, LCRiskLevel.HE, 0.6f, 0.8f, 1.3f, 1.5f),
        };

        // 1. Codex, ritual extraction pool, E.G.O gear profiles, RenderTree, HoldingPlatform plaques, and settings toggles
        foreach (var (name, cat, dmgType, risk, rRed, rWhite, rBlack, rPale) in phase4)
        {
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamed(name, true);
            ThingDef eggDef = DefDatabase<ThingDef>.GetNamed(name + "Egg", true);
            if (AbnormalityUtility.ResolveCodexEntry(raceDef)?.defName != name
                || AbnormalityUtility.ResolveCodexEntry(eggDef)?.defName != name)
            {
                throw new InvalidOperationException($"Phase4 Codex entry resolution failed for {name} / {name}Egg.");
            }

            if (!ExtractUtil.Get_AnomlayLvl2DefList_Ritual(cat).Contains(eggDef))
            {
                throw new InvalidOperationException($"Phase4 {name}Egg missing from {cat} ritual extraction list.");
            }

            if (raceDef.race?.renderTree?.defName != name)
            {
                throw new InvalidOperationException($"Phase4 {name} expected renderTree '{name}', got '{raceDef.race?.renderTree?.defName ?? "<null>"}'.");
            }

            if (ContentFinder<Texture2D>.Get($"UI/HoldingPlatform/{name}_English", reportFailure: false) == null
                || ContentFinder<Texture2D>.Get($"UI/HoldingPlatform/{name}_Simplified Chinese", reportFailure: false) == null)
            {
                throw new InvalidOperationException($"Phase4 HoldingPlatform plaque textures missing for {name}.");
            }

            ThingDef weaponDef = DefDatabase<ThingDef>.GetNamed("EgoWeapon_" + name, true);
            ThingDef suitDef = DefDatabase<ThingDef>.GetNamed("Apparel_" + name + "Suit", true);
            DefDatabase<ThingDef>.GetNamed("Accessory_" + name, true);

            if (!FourColorOverrides.TryGetWeaponProfile(weaponDef, out var wp)
                || wp.damageType != dmgType || wp.riskLevel != risk)
            {
                throw new InvalidOperationException($"EgoWeapon_{name} expected {risk}/{dmgType} profile, got {wp.riskLevel}/{wp.damageType}.");
            }

            if (!FourColorOverrides.TryGetApparelProfile(suitDef, out var ap)
                || ap.riskLevel != risk
                || Math.Abs(ap.red - rRed) > 0.001f
                || Math.Abs(ap.white - rWhite) > 0.001f
                || Math.Abs(ap.black - rBlack) > 0.001f
                || Math.Abs(ap.pale - rPale) > 0.001f)
            {
                throw new InvalidOperationException($"Apparel_{name}Suit expected {risk} {rRed}/{rWhite}/{rBlack}/{rPale}, got {ap.riskLevel} {ap.red}/{ap.white}/{ap.black}/{ap.pale}.");
            }

            FieldInfo toggleField = abnSettings.GetType().GetField("Enable" + name)
                ?? throw new InvalidOperationException($"Missing Phase4 setting toggle Enable{name}.");
            bool prev = (bool)toggleField.GetValue(abnSettings);
            try
            {
                toggleField.SetValue(abnSettings, false);
                if (ExtractUtil.Get_AnomlayLvl2DefList_Ritual(cat).Contains(eggDef))
                {
                    throw new InvalidOperationException($"Enable{name}=false did not filter {name}Egg.");
                }
            }
            finally
            {
                toggleField.SetValue(abnSettings, prev);
            }
        }

        // Verify all 4 ShaderDefs compile, all 28 AnimationDefs exist, and all 20 SoundDefs resolve
        foreach (string shaderName in new[] { "LC_PpodaeSlam", "LC_WoodsmanFurnace", "LC_CosmosSongWave", "LC_ScarecrowDrainBeam" })
        {
            ShaderDef sDef = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderName);
            if (sDef == null || sDef.Shader == null)
            {
                throw new InvalidOperationException($"Phase4 ShaderDef '{shaderName}' failed to resolve or compile.");
            }
        }

        foreach (string animName in new[]
        {
            "Ppodae_Puppy_Idle", "Ppodae_Puppy_Bark", "Ppodae_Puppy_Eat", "Ppodae_Frenzy_Idle", "Ppodae_Frenzy_Walk", "Ppodae_Frenzy_Attack",
            "Woodsman_Room_Idle", "Woodsman_Room_Awaken", "Woodsman_Escape_Idle", "Woodsman_Escape_Walk", "Woodsman_Attack_Chop", "Woodsman_Attack_Frenzy", "Woodsman_EatHeart", "Woodsman_Dead",
            "Cosmos_Contained_Idle", "Cosmos_Transform", "Cosmos_Breached_Idle", "Cosmos_Walk", "Cosmos_Attack", "Cosmos_Sing_Start", "Cosmos_Sing",
            "Scarecrow_Contained_Idle", "Scarecrow_Breached_Idle", "Scarecrow_Walk", "Scarecrow_Attack1", "Scarecrow_Attack2", "Scarecrow_Drink", "Scarecrow_Dead"
        })
        {
            if (DefDatabase<AnimationDef>.GetNamedSilentFail(animName) == null)
            {
                throw new InvalidOperationException($"Phase4 AnimationDef '{animName}' missing.");
            }
        }

        foreach (string sndName in new[]
        {
            "LC_Ppodae_Bark", "LC_Ppodae_Transform", "LC_Ppodae_Atk", "LC_Ppodae_Dead", "LC_Ppodae_Fist",
            "LC_Woodsman_Default", "LC_Woodsman_Escape", "LC_Woodsman_Atk", "LC_Woodsman_Special", "LC_Woodsman_Dead", "LC_Woodsman_AxeHit",
            "LC_Cosmos_Default", "LC_Cosmos_Hit", "LC_Cosmos_Sing", "LC_Cosmos_SpearHit",
            "LC_Scarecrow_Start", "LC_Scarecrow_Atk", "LC_Scarecrow_Drink", "LC_Scarecrow_Dead", "LC_Scarecrow_SpearHit"
        })
        {
            SoundDef sd = DefDatabase<SoundDef>.GetNamed(sndName, true);
            if (sd.subSounds == null || sd.subSounds.Count == 0)
            {
                throw new InvalidOperationException($"Phase4 SoundDef '{sndName}' has no subSounds.");
            }
        }

        FieldInfo peTempField = typeof(CompAbnormality).GetField("PeBoxProducedTemp", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing CompAbnormality.PeBoxProducedTemp field.");

        // 2. Standard containment, PE-Box production, E.G.O extraction, multi-part Spine rendering, and egg-return across all 4 entities
        int pIndex = 0;
        foreach (var (name, _, _, _, _, _, _, _) in phase4)
        {
            Pawn worker = SpawnValidationColonist(map, map.Center);
            worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
            CompPawnStatus emp = worker.GetComp<CompPawnStatus>() ?? throw new InvalidOperationException("Phase4 validation worker missing CompPawnStatus.");
            emp.GetPawnStatusLevel(EPawnStatus.Fortitude).Status = 40;
            emp.GetPawnStatusLevel(EPawnStatus.Prudence).Status = 40;
            emp.GetPawnStatusLevel(EPawnStatus.Temperance).Status = 40;
            emp.GetPawnStatusLevel(EPawnStatus.Justice).Status = 40;

            var plat = SpawnTestContainmentTerminal(map);
            IntVec3 cell = plat.Position;

            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg", true));
            int sig = 0x4c430400 + pIndex++;
            AbnormalityUtility.TransferSignature(egg, sig);
            if (!plat.TryCaptureThing(egg)) throw new InvalidOperationException($"Phase4 egg failed to enter room containment: {name}");

            Pawn held = plat.HeldPawn ?? throw new InvalidOperationException($"Phase4 egg failed to enter containment: {name}");
            CompAbnormality abn = held.GetComp<CompAbnormality>() ?? throw new InvalidOperationException($"Phase4 missing CompAbnormality: {name}");
            if (abn.biosignature != sig) throw new InvalidOperationException($"Phase4 identity mismatch: {name}");

            AbnormalityUtility.Discover(held.def);
            plat.GetGizmos().ToList();
            held.Drawer.renderer.EnsureGraphicsInitialized();
            plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
            plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
            plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);

            plat.CurWorkType = Enum.GetValues(typeof(EAnomalyWorkType)).Cast<EAnomalyWorkType>()
                .OrderByDescending(w => abn.CheckStudierSkillRequire(emp, w)).First();
            float rate = abn.CheckStudierSkillRequire(emp, plat.CurWorkType);
            if (rate <= 0f) throw new InvalidOperationException($"Phase4 worker cannot work on {name}: rate={rate}");

            abn.PeBoxComp.CurAmountIndiPebox = 0;
            peTempField.SetValue(abn, 0);
            Rand.PushState(sig);
            try
            {
                for (int i = 0; i < 24; i++) plat.Notify_StudyInterval(emp);
            }
            finally { Rand.PopState(); }
            if ((int)peTempField.GetValue(abn) <= 0) throw new InvalidOperationException($"Phase4 no interval PeBox produced by {name}");

            // Force Good work tier before Notify_Studied so Qliphoth-1 entities don't breach randomly before EGO/egg-return verification
            peTempField.SetValue(abn, abn.PeBoxComp.Props.amountProdueRangeNormal.max + 1);
            abn.Notify_Studied(worker);
            if (abn.PeBoxComp.CurAmountIndiPebox <= 0) throw new InvalidOperationException($"Phase4 no PeBox spawned by {name}");

            held.GetComp<CompAbnormalityStudiable>().Study(worker, 1000f);
            CompEgoExtractable ego = held.GetComp<CompEgoExtractable>();
            if (ego != null && ego.Props.weaponExtracted != null)
            {
                abn.PeBoxComp.CurAmountIndiPebox = 1000;
                int before = ego.CurAmountWeapon;
                typeof(CompEgoExtractable).GetMethod("ExtractEGO", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ego, new object[] { EGO_TYPE.Weapon });
                if (ego.CurAmountWeapon != before + 1) throw new InvalidOperationException($"Phase4 EGO extraction failed: {name}");
            }

            plat.EjectContents();
            var fx = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Dying" + name, true), cell, map);
            fx.InitWith(held);
            if (held.Spawned) held.DeSpawn();
            Find.WorldPawns.PassToWorld(held, PawnDiscardDecideMode.Discard);
            fx.Complete();
            Thing returnedEgg = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed(name + "Egg", true))
                .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == sig);
            if (returnedEgg == null) throw new InvalidOperationException($"Phase4 identity did not survive egg return: {name}");
            returnedEgg.Destroy();
            CleanupTestContainmentTerminal(plat);
            if (worker.Spawned) worker.DeSpawn();
            if (!worker.Destroyed) worker.Destroy();
        }

        // 3a. Ppodae: non-Instinct entry drop (60% roll), Good result doesn't raise Qliphoth, and breach kill self-returns to PpodaeEgg
        Pawn ppWorker = SpawnValidationColonist(map, map.Center);
        var ppPlat = SpawnTestContainmentTerminal(map);
        Thing ppEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("PpodaeEgg", true));
        if (!ppPlat.TryCaptureThing(ppEgg)) throw new InvalidOperationException("PpodaeEgg failed to enter room containment.");
        CompAbnormality ppAbn = ppPlat.HeldPawn.GetComp<CompAbnormality>();
        ppPlat.CurWorkType = EAnomalyWorkType.Insight;
        ppAbn.QliphothCountCurrent = 2;

        int ppDropSeed = 1;
        for (int s = 1; s <= 100; s++)
        {
            Rand.PushState(s);
            bool hit = Rand.Chance(0.60f);
            Rand.PopState();
            if (hit) { ppDropSeed = s; break; }
        }
        Rand.PushState(ppDropSeed);
        try
        {
            ppAbn.Notify_StudyStart(ppWorker);
            ppAbn.Notify_Studying(ppWorker);
        }
        finally { Rand.PopState(); }
        if (ppAbn.QliphothCountCurrent != 1)
        {
            throw new InvalidOperationException($"Ppodae non-Instinct entry check expected Qliphoth=1, got {ppAbn.QliphothCountCurrent}.");
        }
        ppAbn.GetType().GetMethod("StudyEvent_Good", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ppAbn, new object[] { ppWorker });
        if (ppAbn.QliphothCountCurrent != 1)
        {
            throw new InvalidOperationException($"Ppodae Good work should not raise Qliphoth counter (expected 1, got {ppAbn.QliphothCountCurrent}).");
        }
        CleanupTestContainmentTerminal(ppPlat);

        int ppBreachSig = 0x4c43040A;
        Pawn breachedPpodae = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Ppodae", true), LCContainmentUtility.AbnormalityFaction);
        GenSpawn.Spawn(breachedPpodae, FindFarthestSafeCell(map), map);
        CompAbnormality breachedPpAbn = breachedPpodae.GetComp<CompAbnormality>();
        breachedPpAbn.biosignature = ppBreachSig;
        breachedPpAbn.Notify_Escaped();
        breachedPpAbn.GetType().GetMethod("Notify_SlainHumanlike", BindingFlags.Public | BindingFlags.Instance).Invoke(breachedPpAbn, new object[] { ppWorker });
        MethodInfo ppTickBreached = breachedPpAbn.GetType().GetMethod("TickBreached", BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < 50 && !breachedPpodae.Destroyed; i++)
        {
            ppTickBreached.Invoke(breachedPpAbn, null);
        }
        Thing ppReturned = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("PpodaeEgg", true))
            .FirstOrDefault(t => t.TryGetComp<LC_CompEgg>()?.biosignature == ppBreachSig);
        if (!breachedPpodae.Destroyed || ppReturned == null)
        {
            throw new InvalidOperationException($"Ppodae failed to self-return to PpodaeEgg after humanlike kill: destroyed={breachedPpodae.Destroyed}, eggFound={ppReturned != null}");
        }
        ppReturned.Destroy();
        if (ppWorker.Spawned) ppWorker.DeSpawn();
        if (!ppWorker.Destroyed) ppWorker.Destroy();

        // 3b. Warm-Hearted Woodsman: Temperance III+ arms trap without escaping -> next worker executed -> breach corpse heal -> save/load trap fixture
        Pawn wdWorker = SpawnValidationColonist(map, map.Center);
        wdWorker.GetComp<CompPawnStatus>().GetPawnStatusLevel(EPawnStatus.Temperance).Status = 60;
        var wdPlat = SpawnTestContainmentTerminal(map);
        IntVec3 wdCell = wdPlat.Position;
        Thing wdEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WarmHeartedWoodsmanEgg", true));
        AbnormalityUtility.TransferSignature(wdEgg, 0x4c430410);
        if (!wdPlat.TryCaptureThing(wdEgg)) throw new InvalidOperationException("WarmHeartedWoodsmanEgg failed to enter room containment.");
        Pawn wdPawn = wdPlat.HeldPawn;
        CompAbnormality wdAbn = wdPawn.GetComp<CompAbnormality>();
        PropertyInfo trapArmedProp = wdAbn.GetType().GetProperty("IsHeartTrapArmed", BindingFlags.Public | BindingFlags.Instance);

        if (wdAbn.QliphothCountCurrent != 1 || (bool)trapArmedProp.GetValue(wdAbn))
        {
            throw new InvalidOperationException("WarmHeartedWoodsman initial Qliphoth/trap state unexpected.");
        }

        wdAbn.Notify_StudyStart(wdWorker);
        peTempField.SetValue(wdAbn, wdAbn.PeBoxComp.Props.amountProdueRangeNormal.max + 1);
        wdAbn.Notify_Studied(wdWorker);
        if (wdAbn.QliphothCountCurrent != 0 || !(bool)trapArmedProp.GetValue(wdAbn) || wdAbn.Escaped)
        {
            throw new InvalidOperationException($"WarmHeartedWoodsman Temperance III+ failed to arm silent heart trap: q={wdAbn.QliphothCountCurrent}, armed={trapArmedProp.GetValue(wdAbn)}, escaped={wdAbn.Escaped}");
        }

        Pawn wdVictim = SpawnValidationColonist(map, wdCell);
        wdVictim.apparel?.DestroyAll();
        wdAbn.Notify_StudyStart(wdVictim);
        wdAbn.Notify_Studying(wdVictim);
        if (!wdVictim.Dead || wdVictim.Corpse == null || !wdAbn.Escaped || !wdPawn.Spawned || (bool)trapArmedProp.GetValue(wdAbn))
        {
            throw new InvalidOperationException($"WarmHeartedWoodsman heart trap execution failed: victimDead={wdVictim.Dead}, escaped={wdAbn.Escaped}, spawned={wdPawn.Spawned}, stillArmed={trapArmedProp.GetValue(wdAbn)}");
        }

        BodyPartRecord wdCore = wdPawn.RaceProps.body.corePart;
        float wdTorsoHit = Mathf.Min(35f, wdCore.def.GetMaxHealth(wdPawn) * 0.14f);
        for (int pass = 0; pass < 3 && !wdPawn.Dead && wdPawn.health.summaryHealth.SummaryHealthPercent >= 0.82f; pass++)
        {
            wdPawn.TakeDamage(new DamageInfo(RimWorld.DamageDefOf.Blunt, wdTorsoHit, 2f, -1f, null, wdCore));
            foreach (BodyPartRecord limb in wdCore.parts.Where(p => p.depth == BodyPartDepth.Outside && p.def.defName != "Neck" && p.def.defName != "Head" && p.def.GetMaxHealth(wdPawn) > 40f).Take(4))
            {
                if (wdPawn.Dead || wdPawn.health.summaryHealth.SummaryHealthPercent < 0.82f) break;
                wdPawn.TakeDamage(new DamageInfo(RimWorld.DamageDefOf.Blunt, Mathf.Min(25f, limb.def.GetMaxHealth(wdPawn) * 0.20f), 2f, -1f, null, limb));
            }
        }
        if (wdPawn.Dead)
        {
            throw new InvalidOperationException("Injuring breached WarmHeartedWoodsman for heal check unexpectedly killed it.");
        }
        float wdHpBefore = wdPawn.health.summaryHealth.SummaryHealthPercent;
        if (wdHpBefore >= 0.85f)
        {
            throw new InvalidOperationException($"Failed to injure breached WarmHeartedWoodsman below 85% HP (got {wdHpBefore:F2}).");
        }
        Corpse wdCorpse = wdVictim.Corpse;
        foreach (Thing strayCorpse in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).ToList())
        {
            if (strayCorpse != wdCorpse && !strayCorpse.Destroyed)
            {
                strayCorpse.Destroy();
            }
        }
        object[] seekArgs = { null };
        bool seeksCorpse = (bool)wdAbn.GetType().GetMethod("ShouldSeekCorpseToEat", BindingFlags.Public | BindingFlags.Instance).Invoke(wdAbn, seekArgs);
        if (!seeksCorpse || seekArgs[0] != wdCorpse)
        {
            throw new InvalidOperationException($"WarmHeartedWoodsman ShouldSeekCorpseToEat failed: seeks={seeksCorpse}, found={seekArgs[0]}");
        }
        wdAbn.GetType().GetMethod("ConsumeCorpseHeart", BindingFlags.Public | BindingFlags.Instance).Invoke(wdAbn, new object[] { wdCorpse });
        if (!wdCorpse.Destroyed || wdPawn.health.summaryHealth.SummaryHealthPercent <= wdHpBefore)
        {
            throw new InvalidOperationException($"WarmHeartedWoodsman ConsumeCorpseHeart failed: corpseDestroyed={wdCorpse.Destroyed}, hpBefore={wdHpBefore:F2}, hpAfter={wdPawn.health.summaryHealth.SummaryHealthPercent:F2}");
        }
        if (wdPawn.Spawned) wdPawn.DeSpawn();
        if (!wdPawn.Destroyed) wdPawn.Destroy();
        CleanupTestContainmentTerminal(wdPlat);
        if (wdWorker.Spawned) wdWorker.DeSpawn();
        if (!wdWorker.Destroyed) wdWorker.Destroy();

        // Save/Reload fixture (0x4c430411): leave armed WarmHeartedWoodsman containment room on map for CheckReload
        var savedWdPlat = SpawnTestContainmentTerminal(map);
        Thing savedWdEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WarmHeartedWoodsmanEgg", true));
        AbnormalityUtility.TransferSignature(savedWdEgg, 0x4c430411);
        if (!savedWdPlat.TryCaptureThing(savedWdEgg)) throw new InvalidOperationException("Saved WarmHeartedWoodsmanEgg failed to enter room containment.");
        savedWdPlat.HeldPawn.GetComp<CompAbnormality>().QliphothCountCurrent = 0;
        if (!(bool)trapArmedProp.GetValue(savedWdPlat.HeldPawn.GetComp<CompAbnormality>()))
        {
            throw new InvalidOperationException("Saved WarmHeartedWoodsman fixture failed to arm heart trap.");
        }
        savedWdPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, savedWdPlat.DrawPos);
        savedWdPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, savedWdPlat.DrawPos);
        savedWdPlat.DynamicDrawPhaseAt(DrawPhase.Draw, savedWdPlat.DrawPos);

        // 3c. Fragment of the Universe: 70% Bad no-drop guard at Qliphoth=1, worker panic drop, AoE White song, Cosmic Enlightenment shield
        Pawn cosWorker = SpawnValidationColonist(map, map.Center);
        var cosPlat = SpawnTestContainmentTerminal(map);
        Thing cosEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("FragmentOfTheUniverseEgg", true));
        if (!cosPlat.TryCaptureThing(cosEgg)) throw new InvalidOperationException("FragmentOfTheUniverseEgg failed to enter room containment.");
        CompAbnormality cosPlatAbn = cosPlat.HeldPawn.GetComp<CompAbnormality>();
        cosPlatAbn.QliphothCountCurrent = 1;

        int cosNoDropSeed = 1;
        for (int s = 1; s <= 150; s++)
        {
            Rand.PushState(s);
            bool drop = Rand.Chance(0.70f);
            Rand.PopState();
            if (!drop) { cosNoDropSeed = s; break; }
        }
        Rand.PushState(cosNoDropSeed);
        try
        {
            cosPlatAbn.GetType().GetMethod("StudyEvent_Bad", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cosPlatAbn, new object[] { cosWorker });
        }
        finally { Rand.PopState(); }
        if (cosPlatAbn.QliphothCountCurrent != 1 || cosPlatAbn.Escaped)
        {
            throw new InvalidOperationException($"FragmentOfTheUniverse 30% Bad no-drop roll unexpectedly melted down: q={cosPlatAbn.QliphothCountCurrent}, escaped={cosPlatAbn.Escaped}");
        }

        cosPlatAbn.QliphothCountCurrent = 2;
        Hediff cosPanicMental = cosWorker.health.AddHediff(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
        cosPanicMental.Severity = 0.99f;
        cosPlatAbn.Notify_StudyStart(cosWorker);
        cosPlatAbn.Notify_Studying(cosWorker);
        if (cosPlatAbn.QliphothCountCurrent != 1)
        {
            throw new InvalidOperationException($"FragmentOfTheUniverse panic drop failed (expected 1, got {cosPlatAbn.QliphothCountCurrent}).");
        }
        CleanupTestContainmentTerminal(cosPlat);
        if (cosWorker.Spawned) cosWorker.DeSpawn();
        if (!cosWorker.Destroyed) cosWorker.Destroy();

        Pawn cosBreached = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("FragmentOfTheUniverse", true), LCContainmentUtility.AbnormalityFaction);
        IntVec3 cosCell = FindFarthestSafeCell(map, minColonistDist: 16);
        GenSpawn.Spawn(cosBreached, cosCell, map);
        CompAbnormality cosAbn = cosBreached.GetComp<CompAbnormality>();
        cosAbn.Notify_Escaped();
        MethodInfo cosTickBreached = cosAbn.GetType().GetMethod("TickBreached", BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < 150; i++) cosTickBreached.Invoke(cosAbn, null);

        Pawn songVictim = SpawnValidationColonist(map, FindSafePawnCell(map, cosCell));
        songVictim.apparel?.DestroyAll();
        object[] singArgs = { 0 };
        bool singStarted = (bool)cosAbn.GetType().GetMethod("TryStartSing", BindingFlags.Public | BindingFlags.Instance).Invoke(cosAbn, singArgs);
        if (!singStarted)
        {
            throw new InvalidOperationException("FragmentOfTheUniverse TryStartSing failed after cooldown.");
        }
        for (int i = 0; i < 50; i++) cosTickBreached.Invoke(cosAbn, null);
        Hediff songMental = songVictim.health.hediffSet.GetFirstHediffOfDef(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
        if (songMental == null || songMental.Severity <= 0f)
        {
            throw new InvalidOperationException("FragmentOfTheUniverse cosmic song failed to deal AoE White mental damage.");
        }
        if (songVictim.Spawned) songVictim.DeSpawn();
        if (!songVictim.Destroyed) songVictim.Destroy();
        if (cosBreached.Spawned) cosBreached.DeSpawn();
        if (!cosBreached.Destroyed) cosBreached.Destroy();

        Pawn shieldColonist = SpawnValidationColonist(map, FindFarthestSafeCell(map));
        shieldColonist.apparel?.DestroyAll();
        Type spearWorkerType = GenTypes.GetTypeInAnyAssembly("LCAbnormalities.FragmentOfTheUniverse.DamageWorker_CosmosSpear")
            ?? throw new InvalidOperationException("Missing LCAbnormalities.FragmentOfTheUniverse.DamageWorker_CosmosSpear.");
        spearWorkerType.GetMethod("GrantOrRefreshEnlightenment", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { shieldColonist });
        HediffDef enlDef = DefDatabase<HediffDef>.GetNamed("LC_CosmicEnlightenment", true);
        Hediff enlHediff = shieldColonist.health.hediffSet.GetFirstHediffOfDef(enlDef)
            ?? throw new InvalidOperationException("GrantOrRefreshEnlightenment did not add LC_CosmicEnlightenment.");
        object enlComp = (enlHediff as HediffWithComps)?.comps?.FirstOrDefault(c => c.GetType().Name == "HediffComp_CosmicEnlightenment")
            ?? throw new InvalidOperationException("Missing HediffComp_CosmicEnlightenment comp.");
        PropertyInfo enlRemainProp = enlComp.GetType().GetProperty("RemainingShield", BindingFlags.Public | BindingFlags.Instance);
        float beforeShield = (float)enlRemainProp.GetValue(enlComp);
        shieldColonist.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 20f));
        float afterShield = (float)enlRemainProp.GetValue(enlComp);
        if (Math.Abs(beforeShield - 50f) > 0.1f || afterShield > 31f || shieldColonist.health.hediffSet.HasHediff(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage))
        {
            throw new InvalidOperationException($"Cosmic Enlightenment shield absorption failed: before={beforeShield:F1}, after={afterShield:F1}.");
        }
        if (shieldColonist.Spawned) shieldColonist.DeSpawn();
        if (!shieldColonist.Destroyed) shieldColonist.Destroy();

        // 3d. Scarecrow Searching for Wisdom: Prudence III+ immediate breach, Brain Drain full invulnerability + healing
        Pawn scWorker = SpawnValidationColonist(map, map.Center);
        scWorker.GetComp<CompPawnStatus>().GetPawnStatusLevel(EPawnStatus.Prudence).Status = 60;
        var scPlat = SpawnTestContainmentTerminal(map);
        Thing scEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ScarecrowSearchingForWisdomEgg", true));
        if (!scPlat.TryCaptureThing(scEgg)) throw new InvalidOperationException("ScarecrowEgg failed to enter room containment.");
        Pawn scPawn = scPlat.HeldPawn;
        CompAbnormality scAbn = scPawn.GetComp<CompAbnormality>();

        scAbn.Notify_StudyStart(scWorker);
        peTempField.SetValue(scAbn, scAbn.PeBoxComp.Props.amountProdueRangeNormal.max + 1);
        scAbn.Notify_Studied(scWorker);
        if (!scAbn.Escaped || !scPawn.Spawned)
        {
            throw new InvalidOperationException($"Scarecrow Prudence III+ study failed to breach: escaped={scAbn.Escaped}, spawned={scPawn.Spawned}");
        }

        BodyPartRecord scCore = scPawn.RaceProps.body.corePart;
        scPawn.TakeDamage(new DamageInfo(RimWorld.DamageDefOf.Blunt, Mathf.Min(35f, scCore.def.GetMaxHealth(scPawn) * 0.15f), 2f, -1f, null, scCore));
        if (scPawn.Dead)
        {
            throw new InvalidOperationException("Injuring breached Scarecrow for Brain Drain check unexpectedly killed it.");
        }
        float scHpBeforeDrain = scPawn.health.summaryHealth.SummaryHealthPercent;
        if (scHpBeforeDrain >= 0.999f)
        {
            throw new InvalidOperationException($"Failed to injure breached Scarecrow before Brain Drain check (hp={scHpBeforeDrain:F2}).");
        }

        scAbn.GetType().GetMethod("StartBrainDrain", BindingFlags.Public | BindingFlags.Instance).Invoke(scAbn, new object[] { scWorker });
        bool isDraining = (bool)scAbn.GetType().GetProperty("IsDrainingBrain", BindingFlags.Public | BindingFlags.Instance).GetValue(scAbn);
        if (!isDraining || scPawn.Drawer?.renderer?.CurAnimation?.defName != "Scarecrow_Drink")
        {
            throw new InvalidOperationException($"Scarecrow StartBrainDrain animation/state check failed: draining={isDraining}, anim={scPawn.Drawer?.renderer?.CurAnimation?.defName ?? "<null>"}");
        }

        scPawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 50f, 2f, -1f, scWorker));
        if (Math.Abs(scPawn.health.summaryHealth.SummaryHealthPercent - scHpBeforeDrain) > 0.0001f)
        {
            throw new InvalidOperationException($"Scarecrow was not invulnerable during Brain Drain: before={scHpBeforeDrain:F4}, after={scPawn.health.summaryHealth.SummaryHealthPercent:F4}");
        }

        MethodInfo scTickBreached = scAbn.GetType().GetMethod("TickBreached", BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < 35; i++) scTickBreached.Invoke(scAbn, null);
        if (scPawn.health.summaryHealth.SummaryHealthPercent <= scHpBeforeDrain)
        {
            throw new InvalidOperationException($"Scarecrow Brain Drain failed to heal HP: before={scHpBeforeDrain:F4}, after={scPawn.health.summaryHealth.SummaryHealthPercent:F4}");
        }

        if (scPawn.Spawned) scPawn.DeSpawn();
        if (!scPawn.Destroyed) scPawn.Destroy();
        CleanupTestContainmentTerminal(scPlat);
        if (scWorker.Spawned) scWorker.DeSpawn();
        if (!scWorker.Destroyed) scWorker.Destroy();

        Log.Message("[LC17 Validation] GAMEPLAY PASS; Phase4; 4_abnormalities/ppodae_return/woodsman_hearttrap/cosmos_song_enlightenment/scarecrow_braindrain");
    }

    private static void ValidatePhase4AfterLoad(Map map)
    {
        bool foundSavedWoodsmanTrap = false;
        foreach (Building_AbnormalityContainmentTerminal plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>())
        {
            if (plat.HeldPawn?.def?.defName == "WarmHeartedWoodsman"
                && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430411)
            {
                CompAbnormality wdAbn = plat.HeldPawn.GetComp<CompAbnormality>();
                bool armed = (bool)(wdAbn?.GetType().GetProperty("IsHeartTrapArmed", BindingFlags.Public | BindingFlags.Instance)?.GetValue(wdAbn) ?? false);
                if (!armed || wdAbn.Escaped)
                {
                    throw new InvalidOperationException($"Saved WarmHeartedWoodsman heartTrapArmed state lost across reload: armed={armed}, escaped={wdAbn?.Escaped}");
                }

                plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);
                foundSavedWoodsmanTrap = true;
                break;
            }
        }

        if (!foundSavedWoodsmanTrap)
        {
            throw new InvalidOperationException("Saved WarmHeartedWoodsman containment terminal (0x4c430411) missing after reload.");
        }
    }

    private static CellRect FindClearRoomRect(Map map, int size)
    {
        for (int x = 12; x < map.Size.x - 12 - size; x += 2)
        {
            for (int z = 12; z < map.Size.z - 12 - size; z += 2)
            {
                CellRect rect = new CellRect(x, z, size, size);
                bool ok = true;
                foreach (IntVec3 c in rect.ExpandedBy(1).Cells)
                {
                    if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetRegion(map) == null
                        || c.GetEdifice(map) != null || c.GetFirstBuilding(map) != null || c.GetFirstPawn(map) != null
                        || !c.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                {
                    return rect;
                }
            }
        }

        // Pass 2 fallback: clear natural plants, filth, rock chunks, or mineable outcrops while never disturbing pawns, items, corpses, or buildings.
        for (int x = 12; x < map.Size.x - 12 - size; x += 2)
        {
            for (int z = 12; z < map.Size.z - 12 - size; z += 2)
            {
                CellRect rect = new CellRect(x, z, size, size);
                bool ok = true;
                foreach (IntVec3 c in rect.ExpandedBy(1).Cells)
                {
                    if (!c.InBounds(map) || c.Fogged(map) || c.GetFirstPawn(map) != null)
                    {
                        ok = false;
                        break;
                    }

                    RoofDef roof = c.GetRoof(map);
                    if (roof != null && roof.isThickRoof)
                    {
                        ok = false;
                        break;
                    }

                    List<Thing> things = c.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing t = things[i];
                        bool clearableNatural = t.def.category == ThingCategory.Plant
                            || t.def.category == ThingCategory.Filth
                            || t.def.mineable
                            || (t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.Chunks));
                        if (!clearableNatural)
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (!ok)
                    {
                        break;
                    }
                }
                if (ok)
                {
                    return rect;
                }
            }
        }

        throw new InvalidOperationException($"Could not find a clear {size}x{size} area for room containment test.");
    }

    private static void ClearCellForTestRoom(Map map, IntVec3 c)
    {
        if (!c.InBounds(map))
        {
            return;
        }

        List<Thing> things = c.GetThingList(map);
        for (int i = things.Count - 1; i >= 0; i--)
        {
            Thing t = things[i];
            if (!t.Destroyed && (t.def.category == ThingCategory.Plant
                || t.def.category == ThingCategory.Filth
                || t.def.mineable
                || (t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.Chunks))))
            {
                t.Destroy();
            }
        }

        if (!c.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
        {
            map.terrainGrid?.SetTerrain(c, TerrainDefOf.Concrete);
        }
    }

    private static Room BuildTestContainmentRoom(Map map, out CellRect roomRect, out Building_LCContainmentDoor door)
    {
        roomRect = FindClearRoomRect(map, 5);
        ThingDef wallDef = DefDatabase<ThingDef>.GetNamed("LC_ContainmentWall", true);
        ThingDef doorDef = DefDatabase<ThingDef>.GetNamed("LC_ContainmentDoor", true);
        IntVec3 doorCell = new IntVec3(roomRect.CenterCell.x, 0, roomRect.minZ);
        door = null;

        foreach (IntVec3 c in roomRect.Cells)
        {
            ClearCellForTestRoom(map, c);
        }
        ClearCellForTestRoom(map, doorCell + IntVec3.South);

        foreach (IntVec3 c in roomRect.EdgeCells)
        {
            Thing building = c == doorCell
                ? ThingMaker.MakeThing(doorDef, RimWorld.ThingDefOf.Steel)
                : ThingMaker.MakeThing(wallDef, RimWorld.ThingDefOf.Steel);
            building.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(building, c, map);
            if (building is Building_LCContainmentDoor lcDoor)
            {
                door = lcDoor;
            }
        }

        foreach (IntVec3 c in roomRect.Cells)
        {
            map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
        }

        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        return roomRect.CenterCell.GetRoom(map)
            ?? throw new InvalidOperationException("Failed to form enclosed containment room.");
    }

    private static void ValidateRoomContainment(Map map)
    {
        Pawn worker = SpawnValidationColonist(map, map.Center);
        CompPawnStatus employee = worker.GetComp<CompPawnStatus>()
            ?? throw new InvalidOperationException("SpawnValidationColonist missing CompPawnStatus.");
        employee.SetPawnStatusEnabled(true);
        Pawn nonEmployee = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
            canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f));
        GenSpawn.Spawn(nonEmployee, FindSafePawnCell(map, map.Center), map);
        CompPawnStatus nonEmpStatus = nonEmployee.GetComp<CompPawnStatus>();
        if (nonEmpStatus != null && nonEmpStatus.Enabled)
        {
            throw new InvalidOperationException("Freshly generated colonist unexpectedly had CompPawnStatus.Enabled=true.");
        }

        ThingDef terminalDef = DefDatabase<ThingDef>.GetNamed("LC_ContainmentTerminal", true);

        // 1) Enclosed room + LC_ContainmentDoor + RoomRoleDef + duplicate terminal guard + static abnormality (OneSin)
        Room room1 = BuildTestContainmentRoom(map, out CellRect rect1, out Building_LCContainmentDoor door1);
        IntVec3 term1Pos = new IntVec3(rect1.minX + 1, 0, rect1.maxZ - 1);
        var terminal1 = (Building_AbnormalityContainmentTerminal)ThingMaker.MakeThing(terminalDef);
        terminal1.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(terminal1, term1Pos, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        room1 = rect1.CenterCell.GetRoom(map);

        if (room1.Role?.defName != "LC_ContainmentRoom")
        {
            throw new InvalidOperationException($"Expected room1.Role == LC_ContainmentRoom, got {room1.Role?.defName ?? "<null>"}.");
        }

        if (!door1.PawnCanOpen(nonEmployee))
        {
            throw new InvalidOperationException("Empty containment room door should allow non-employee colonist passage.");
        }

        if (!terminal1.IsRoomValidForContainment(out string invalidReason))
        {
            throw new InvalidOperationException($"Terminal1 room expected valid, got: {invalidReason}");
        }

        var placeWorker = new PlaceWorker_AbnormalityContainmentTerminal();
        IntVec3 dupPos = new IntVec3(rect1.maxX - 1, 0, rect1.maxZ - 1);
        AcceptanceReport dupReport = placeWorker.AllowsPlacing(terminalDef, dupPos, Rot4.North, map);
        if (dupReport.Accepted)
        {
            throw new InvalidOperationException("PlaceWorker_AbnormalityContainmentTerminal failed to reject duplicate terminal in the same room.");
        }

        Thing oneSinEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("OneSinEgg", true));
        AbnormalityUtility.TransferSignature(oneSinEgg, 0x4c430901);
        if (!terminal1.TryCaptureThing(oneSinEgg))
        {
            throw new InvalidOperationException("terminal1.TryCaptureThing(OneSinEgg) returned false.");
        }

        Pawn oneSinHeld = terminal1.HeldPawn ?? throw new InvalidOperationException("terminal1.HeldPawn is null after capturing OneSinEgg.");
        CompAbnormalityHoldingPlatformTarget oneSinTarget = oneSinHeld.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
        if (!oneSinHeld.Spawned || oneSinHeld.GetRoom() != room1 || oneSinTarget == null || !oneSinTarget.CurrentlyContainedInRoom)
        {
            throw new InvalidOperationException("OneSin is not properly spawned and contained inside room1.");
        }
        if (GenHostility.HostileTo(worker, oneSinHeld) || GenHostility.HostileTo(oneSinHeld, worker) || !oneSinHeld.ThreatDisabled(worker))
        {
            throw new InvalidOperationException("Room-contained OneSin must be non-hostile and ThreatDisabled for employees.");
        }

        if (!door1.PawnCanOpen(worker) || door1.PawnCanOpen(nonEmployee) || door1.PawnCanOpen(oneSinHeld))
        {
            throw new InvalidOperationException(
                $"LC_ContainmentDoor access check failed: worker={door1.PawnCanOpen(worker)}, nonEmp={door1.PawnCanOpen(nonEmployee)}, oneSin={door1.PawnCanOpen(oneSinHeld)}");
        }
        door1.restrictToEmployees = false;
        if (!door1.PawnCanOpen(nonEmployee))
        {
            throw new InvalidOperationException("LC_ContainmentDoor failed to allow non-employee when restrictToEmployees=false.");
        }
        door1.restrictToEmployees = true;
        door1.GetGizmos().ToList();
        door1.GetInspectString();

        CompAbnormalityStudiable oneSinStudy = oneSinHeld.GetComp<CompAbnormalityStudiable>()
            ?? throw new InvalidOperationException("OneSin missing CompAbnormalityStudiable.");
        oneSinStudy.lastStudiedTick = Find.TickManager.TicksGame - oneSinStudy.Props.frequencyTicks;

        var workGiver = new LCAnomalyCore.Jobs.WorkGiver_AbnormalityStudyInteract();
        if (!workGiver.HasJobOnThing(worker, terminal1, forced: true))
        {
            throw new InvalidOperationException($"WorkGiver_AbnormalityStudyInteract.HasJobOnThing returned false for room-contained OneSin. failReason={JobFailReason.Reason ?? "<none>"}");
        }
        Job studyJob = workGiver.JobOnThing(worker, terminal1, forced: true);
        if (studyJob == null || studyJob.targetA.Thing != terminal1 || studyJob.targetC.Thing != oneSinHeld)
        {
            throw new InvalidOperationException("Study job targets mismatch for room terminal.");
        }

        foreach (string hudTexPath in new[]
        {
            "UI/ContainmentRoom/RoomHeaderFrame",
            "UI/ContainmentRoom/QliphothFrame",
            "UI/ContainmentRoom/QliphothAlertFrame",
            "UI/ContainmentRoom/PeBoxIcon",
            "UI/ContainmentRoom/EnergyBarFrame",
            "UI/ContainmentRoom/EnergyCube_PE",
            "UI/ContainmentRoom/EnergyCube_NE",
            "UI/ContainmentRoom/EnergyCube_Empty",
            "UI/ContainmentRoom/ResultBadge_Good",
            "UI/ContainmentRoom/ResultBadge_Normal",
            "UI/ContainmentRoom/ResultBadge_Bad",
            "Things/Building/LC_ContainmentTerminal/ContainmentTerminal",
            "Things/Building/LC_ContainmentDoor/ContainmentDoor_Mover",
            "Things/Building/LC_ContainmentDoor/ContainmentDoor_MenuIcon"
        })
        {
            if (ContentFinder<Texture2D>.Get(hudTexPath, false) == null)
            {
                throw new InvalidOperationException($"Missing containment room texture asset: {hudTexPath}");
            }
        }

        terminal1.CurWorkType = EAnomalyWorkType.Attachment;
        terminal1.Notify_StudyStart(worker);
        if (!door1.IsLockedForActiveWork || door1.PawnCanOpen(worker) || door1.PawnCanOpen(nonEmployee) || door1.PawnCanOpen(oneSinHeld) || door1.FreePassage)
        {
            throw new InvalidOperationException(
                $"door1 should be locked during active work: locked={door1.IsLockedForActiveWork}, workerOpen={door1.PawnCanOpen(worker)}, freePassage={door1.FreePassage}");
        }
        door1.GetInspectString();
        terminal1.Notify_Studying(worker);
        Rand.PushState(0x4c430901);
        try
        {
            for (int i = 0; i < 12; i++) terminal1.Notify_StudyInterval(employee);
        }
        finally { Rand.PopState(); }

        if (!terminal1.IsStudySessionActive
            || terminal1.ActiveSessionMaxBoxes <= 0
            || terminal1.ActiveSessionPeCount + terminal1.ActiveSessionNeCount != 12)
        {
            throw new InvalidOperationException(
                $"Room HUD active study metrics mismatch: active={terminal1.IsStudySessionActive}, PE={terminal1.ActiveSessionPeCount}, NE={terminal1.ActiveSessionNeCount}, max={terminal1.ActiveSessionMaxBoxes}");
        }
        terminal1.GetInspectString();
        terminal1.DrawGUIOverlay();

        CompAbnormality oneSinAbn = oneSinHeld.GetComp<CompAbnormality>();
        oneSinAbn.Notify_Studied(worker);
        if (oneSinAbn.PeBoxComp.CurAmountIndiPebox <= 0)
        {
            throw new InvalidOperationException("Room-contained OneSin produced 0 PE-Boxes.");
        }
        if (terminal1.IsStudySessionActive || !terminal1.LastStudyResult.HasValue)
        {
            throw new InvalidOperationException(
                $"Room HUD did not finalize study session cleanly: active={terminal1.IsStudySessionActive}, result={terminal1.LastStudyResult?.ToString() ?? "<null>"}");
        }
        if (door1.IsLockedForActiveWork || !door1.PawnCanOpen(worker))
        {
            throw new InvalidOperationException($"door1 failed to unlock when work ended: locked={door1.IsLockedForActiveWork}, workerOpen={door1.PawnCanOpen(worker)}");
        }

        terminal1.GetGizmos().ToList();
        terminal1.GetInspectString();
        terminal1.DrawGUIOverlay();

        // 1b) Work assignment preview dialog + Qliphoth Meltdown wave/clear/breach validation
        if (LCAnomalyCore.Defs.SoundDefOf.LC_Sound_QliphothMeltdown_Alert == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_QliphothMeltdown_Clear == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_QliphothMeltdown_Fail == null)
        {
            throw new InvalidOperationException("One or more LC_Sound_QliphothMeltdown_* SoundDefs failed DefOf binding.");
        }

        var workDialog = new Dialog_LC_AssignWorkType(terminal1);
        foreach (EAnomalyWorkType wt in Enum.GetValues(typeof(EAnomalyWorkType)))
        {
            if (wt == EAnomalyWorkType.Unknown) continue;
            float previewRate = Dialog_LC_AssignWorkType.ComputeSuccessRatePreview(oneSinAbn, employee, wt);
            string tierKey = Dialog_LC_AssignWorkType.GetSuccessTierKey(previewRate);
            if (previewRate < 0f || previewRate > 1f || !tierKey.CanTranslate())
            {
                throw new InvalidOperationException($"Work preview rate/tier invalid for {wt}: rate={previewRate}, key={tierKey}");
            }
        }

        MapComponent_LCQliphothMeltdown meltdownMgr = map.GetComponent<MapComponent_LCQliphothMeltdown>()
            ?? throw new InvalidOperationException("MapComponent_LCQliphothMeltdown missing on map.");
        bool prevSettingInRoomTest = Setting_LCAnomalyCore_Main.Settings?.If_EnableQliphothMeltdown ?? true;
        try
        {
            if (Setting_LCAnomalyCore_Main.Settings != null)
            {
                Setting_LCAnomalyCore_Main.Settings.If_EnableQliphothMeltdown = true;
            }
            meltdownMgr.ResetAllMeltdowns(resetWaveProgress: true);

            // Verify automatic facility-wide meltdown wave trigger after WorksPerWave completed works
            int worksNeeded = meltdownMgr.WorksPerWave;
            for (int w = 0; w < worksNeeded; w++)
            {
                terminal1.Notify_StudyFinished(LC_StudyResult.Good);
            }
            if (meltdownMgr.MeltdownLevel < 1 || meltdownMgr.ActiveMeltdownCount < 1)
            {
                throw new InvalidOperationException($"Automatic Qliphoth Meltdown wave did not trigger after {worksNeeded} works: level={meltdownMgr.MeltdownLevel}, active={meltdownMgr.ActiveMeltdownCount}");
            }
            meltdownMgr.ResetAllMeltdowns(resetWaveProgress: true);

            // Verify cooldown bypass + urgent WorkGiver priority + immediate clear on Notify_StudyStart
            oneSinHeld.GetComp<CompAbnormalityStudiable>().lastStudiedTick = Find.TickManager.TicksGame;
            if (oneSinHeld.GetComp<CompAbnormalityStudiable>().CurrentlyStudiable())
            {
                throw new InvalidOperationException("OneSin should still be on cooldown before meltdown triggers.");
            }
            if (!terminal1.TriggerMeltdown(120) || !terminal1.IsInMeltdown)
            {
                throw new InvalidOperationException("terminal1.TriggerMeltdown(120) failed.");
            }
            if (!oneSinHeld.GetComp<CompAbnormalityStudiable>().CurrentlyStudiable())
            {
                throw new InvalidOperationException("Meltdown did not bypass study cooldown in CurrentlyStudiable().");
            }
            var studyWorkGiver = new LCAnomalyCore.Jobs.WorkGiver_AbnormalityStudyInteract();
            if (studyWorkGiver.GetPriority(worker, terminal1) < 1000000f)
            {
                throw new InvalidOperationException("Meltdown WorkGiver priority did not elevate above 1000000.");
            }
            if (!studyWorkGiver.HasJobOnThing(worker, terminal1, forced: false))
            {
                throw new InvalidOperationException("Meltdown did not allow emergency employee response when no pawns were pre-assigned.");
            }
            terminal1.GetInspectString();
            terminal1.DrawGUIOverlay();
            meltdownMgr.MapComponentOnGUI();
            terminal1.Notify_StudyStart(worker);
            if (terminal1.IsInMeltdown)
            {
                throw new InvalidOperationException("terminal1 meltdown did not clear immediately on Notify_StudyStart.");
            }
            terminal1.Notify_StudySessionEnded();
        }
        finally
        {
            terminal1.ClearMeltdown(resolvedByWork: false);
            meltdownMgr.ResetAllMeltdowns(resetWaveProgress: true);
            if (Setting_LCAnomalyCore_Main.Settings != null)
            {
                Setting_LCAnomalyCore_Main.Settings.If_EnableQliphothMeltdown = prevSettingInRoomTest;
            }
        }

        // 1c) Facility Control MainTab + Story/Ordeals meltdown & warning bridges
        if (DefDatabase<MainButtonDef>.GetNamedSilentFail("LC_FacilityControl") == null)
        {
            throw new InvalidOperationException("MainButtonDef LC_FacilityControl is missing.");
        }
        var facilityTab = new LCAnomalyCore.UI.MainTabWindow_LCFacility();
        if (facilityTab.InitialSize.x < 900f)
        {
            throw new InvalidOperationException("MainTabWindow_LCFacility InitialSize unexpected.");
        }
        if (MapComponent_LCQliphothMeltdown.NextMilestonePreviewProvider == null)
        {
            throw new InvalidOperationException("MeltdownOrdealBridge did not register NextMilestonePreviewProvider.");
        }
        Type ordealBridgeType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("LCAnomalyOrdeals.Meltdown.GameComponent_OrdealWarningBridge", false))
            .FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("GameComponent_OrdealWarningBridge type not found.");
        MethodInfo warnPtsMethod = ordealBridgeType.GetMethod("GetWarningPointsForOrdealKind", BindingFlags.Public | BindingFlags.Static);
        if ((int)warnPtsMethod.Invoke(null, new object[] { "LCOrdeal_AmberDawn" }) != 15
            || (int)warnPtsMethod.Invoke(null, new object[] { "LCOrdeal_CrimsonNoon" }) != 35
            || (int)warnPtsMethod.Invoke(null, new object[] { "LCOrdeal_AmberDusk" }) != 55
            || (int)warnPtsMethod.Invoke(null, new object[] { "LCOrdeal_GreenMidnight" }) != 85
            || (int)warnPtsMethod.Invoke(null, new object[] { "LCOrdeal_WhiteClaw" }) != 85)
        {
            throw new InvalidOperationException("GameComponent_OrdealWarningBridge returned unexpected tier warning points.");
        }
        Type storyCompAccessType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("LCAnomalyStory.StoryComponents", false))
            .FirstOrDefault(t => t != null);
        object devComp = storyCompAccessType?.GetProperty("Development", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (devComp != null)
        {
            MethodInfo getStat = devComp.GetType().GetMethod("GetStatistic", BindingFlags.Public | BindingFlags.Instance);
            long waveStats = (long)getStat.Invoke(devComp, new object[] { "meltdownWaves" });
            long resolvedStats = (long)getStat.Invoke(devComp, new object[] { "meltdownsResolved" });
            if (waveStats < 1 || resolvedStats < 1)
            {
                throw new InvalidOperationException($"LCAnomalyStory did not record meltdown statistics: waves={waveStats}, resolved={resolvedStats}");
            }
        }

        // 2) Wandering biological abnormality (PunishingBird): Three-Birds count guard + wander + escape restores hostility
        Room room2 = BuildTestContainmentRoom(map, out CellRect rect2, out Building_LCContainmentDoor door2);
        IntVec3 term2Pos = new IntVec3(rect2.minX + 1, 0, rect2.maxZ - 1);
        var terminal2 = (Building_AbnormalityContainmentTerminal)ThingMaker.MakeThing(terminalDef);
        terminal2.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(terminal2, term2Pos, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        room2 = rect2.CenterCell.GetRoom(map);

        Assembly abnAsm = oneSinAbn.GetType().Assembly;
        MethodInfo countForestBirds = abnAsm.GetType("LCAbnormalities.Shared.LCAbnormalitiesEvents", throwOnError: true)
            .GetMethod("CountBreachedForestBirds", BindingFlags.Public | BindingFlags.Static);
        int breachedBeforeBird = (int)countForestBirds.Invoke(null, new object[] { map });

        Thing birdEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("PunishingBirdEgg", true));
        AbnormalityUtility.TransferSignature(birdEgg, 0x4c430902);
        if (!terminal2.TryCaptureThing(birdEgg))
        {
            throw new InvalidOperationException("terminal2.TryCaptureThing(PunishingBirdEgg) returned false.");
        }

        Pawn bird = terminal2.HeldPawn ?? throw new InvalidOperationException("terminal2.HeldPawn is null after capturing PunishingBirdEgg.");
        CompAbnormalityHoldingPlatformTarget birdTarget = bird.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
        CompAbnormality birdAbn = bird.GetComp<CompAbnormality>();
        if (!bird.Spawned || bird.GetRoom() != room2 || !birdTarget.CurrentlyContainedInRoom || birdAbn.Escaped)
        {
            throw new InvalidOperationException("PunishingBird is not properly contained inside room2.");
        }
        if (GenHostility.HostileTo(worker, bird) || GenHostility.HostileTo(bird, worker) || !bird.ThreatDisabled(worker))
        {
            throw new InvalidOperationException("Room-contained PunishingBird must be non-hostile and ThreatDisabled.");
        }
        if (EscapedAbnormalityOverlayDrawer.ShouldDrawEscapedOverlay(bird))
        {
            throw new InvalidOperationException("Room-contained PunishingBird must not draw escaped bottom overlay.");
        }
        if (door2.PawnCanOpen(bird))
        {
            throw new InvalidOperationException("Room-contained PunishingBird must not be able to open LC_ContainmentDoor.");
        }
        if ((int)countForestBirds.Invoke(null, new object[] { map }) != breachedBeforeBird)
        {
            throw new InvalidOperationException("Room-contained PunishingBird was falsely counted as breached by CountBreachedForestBirds.");
        }

        if (birdAbn.Props.canWanderInContainmentRoom)
        {
            throw new InvalidOperationException("PunishingBird must have canWanderInContainmentRoom=false.");
        }

        IntVec3 birdStartCell = bird.Position;
        bird.jobs.CheckForJobOverride();
        for (int i = 0; i < 30; i++) Find.TickManager.DoSingleTick();
        if (!birdTarget.CurrentlyContainedInRoom || birdAbn.Escaped || bird.GetRoom() != room2)
        {
            throw new InvalidOperationException("Room-contained PunishingBird unexpectedly escaped or left room2 during ticks.");
        }
        if (bird.Position != birdStartCell || (bird.CurJob != null && (bird.CurJob.def == RimWorld.JobDefOf.GotoWander || bird.CurJob.def == RimWorld.JobDefOf.Wait_Wander)))
        {
            throw new InvalidOperationException(
                $"Stationary PunishingBird unexpectedly wandered in containment: start={birdStartCell}, now={bird.Position}, job={bird.CurJob?.def?.defName ?? "<null>"}.");
        }
        if (bird.CurJob != null && bird.CurJob.canBashDoors)
        {
            throw new InvalidOperationException("Room-contained PunishingBird job must not allow bashing doors.");
        }

        terminal2.Notify_StudyStart(worker);
        if (!door2.IsLockedForActiveWork || door2.PawnCanOpen(worker) || door2.PawnCanOpen(bird))
        {
            throw new InvalidOperationException($"door2 should lock during active work on PunishingBird: locked={door2.IsLockedForActiveWork}");
        }

        birdTarget.Escape();
        if (birdTarget.CurrentlyContainedInRoom || terminal2.HeldPawn != null || !birdAbn.Escaped)
        {
            throw new InvalidOperationException("PunishingBird Escape() did not release from terminal2.");
        }
        if (door2.IsLockedForActiveWork || !bird.CanOpenAnyDoor || !door2.PawnCanOpen(bird) || door2.BlocksPawn(bird) || !door2.PawnCanOpen(worker))
        {
            throw new InvalidOperationException(
                $"Breached PunishingBird door/unlock checks failed: locked={door2.IsLockedForActiveWork}, canOpenAnyDoor={bird.CanOpenAnyDoor}, doorPawnCanOpen={door2.PawnCanOpen(bird)}, blocksBird={door2.BlocksPawn(bird)}, workerOpen={door2.PawnCanOpen(worker)}");
        }
        if (!GenHostility.HostileTo(worker, bird) || bird.ThreatDisabled(worker))
        {
            throw new InvalidOperationException("Escaped PunishingBird did not restore hostility / ThreatDisabled=false.");
        }
        if ((int)countForestBirds.Invoke(null, new object[] { map }) != breachedBeforeBird + 1)
        {
            throw new InvalidOperationException("Escaped PunishingBird was not counted by CountBreachedForestBirds.");
        }
        if (!EscapedAbnormalityOverlayDrawer.ShouldDrawEscapedOverlay(bird)
            || EscapedAbnormalityOverlayDrawer.GetRiskTag(bird) != "TETH"
            || EscapedAbnormalityOverlayDrawer.GetHealthPercent(bird) <= 0f)
        {
            throw new InvalidOperationException(
                $"Escaped PunishingBird overlay check failed: shouldDraw={EscapedAbnormalityOverlayDrawer.ShouldDrawEscapedOverlay(bird)}, risk={EscapedAbnormalityOverlayDrawer.GetRiskTag(bird) ?? "<null>"}, hp={EscapedAbnormalityOverlayDrawer.GetHealthPercent(bird)}");
        }
        EscapedAbnormalityOverlayDrawer.DrawEscapedOverlay(bird);

        // Verify working SoundDefs & work narration resolution/spawning
        if (LCAnomalyCore.Defs.SoundDefOf.LC_Sound_ContainmentWork_Start == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_ContainmentWork_Finish == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_ContainmentWork_DefaultLoop == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_ContainmentWork_PeBox == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_ContainmentWork_NeBox == null)
        {
            throw new InvalidOperationException("One or more LC_Sound_ContainmentWork_* SoundDefs failed DefOf binding.");
        }
        foreach (ThingDef abnDef in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            CompProperties_Abnormality abnProps = abnDef.GetCompProperties<CompProperties_Abnormality>();
            if (abnProps != null && abnDef.GetCompProperties<CompProperties_PeBoxProduce>() != null)
            {
                SoundDef resolvedWorkSound = abnProps.soundWorking ?? LCAnomalyCore.Defs.SoundDefOf.LC_Sound_ContainmentWork_DefaultLoop;
                if (resolvedWorkSound == null)
                {
                    throw new InvalidOperationException($"Workable abnormality {abnDef.defName} has neither soundWorking nor DefaultLoop.");
                }
            }
        }
        foreach (string testAbnName in new[] { "OneSin", "PunishingBird", "AllAroundHelper", "NothingThere" })
        {
            Pawn sampleAbn = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(testAbnName, true));
            try
            {
                string startLine = WorkNarrationUtility.ResolveStartLine(worker, sampleAbn, EAnomalyWorkType.Instinct, "Instinct");
                string midLine = WorkNarrationUtility.ResolveMidLine(worker, sampleAbn, EAnomalyWorkType.Insight, "Insight", 0);
                string resLine = WorkNarrationUtility.ResolveResultLine(worker, sampleAbn, LC_StudyResult.Good);
                foreach (string line in new[] { startLine, midLine, resLine })
                {
                    if (string.IsNullOrWhiteSpace(line)
                        || line.Contains("{0}")
                        || line.Contains("{1}")
                        || line.Contains("LC_WorkNarration_"))
                    {
                        throw new InvalidOperationException($"Invalid work narration line for {testAbnName}: '{line ?? "<null>"}'");
                    }
                }
                if (testAbnName == "PunishingBird")
                {
                    Mote_LCWorkNarration spawnedMote = WorkNarrationUtility.SpawnNarration(map, terminal2.DrawPos, midLine, WorkNarrationUtility.GetWorkTypeAccentColor(EAnomalyWorkType.Insight));
                    if (spawnedMote == null || !spawnedMote.Spawned)
                    {
                        throw new InvalidOperationException("WorkNarrationUtility.SpawnNarration failed to spawn Mote_LCWorkNarration.");
                    }
                    spawnedMote.Destroy();
                }
            }
            finally
            {
                if (sampleAbn != null && !sampleAbn.Destroyed)
                {
                    sampleAbn.Destroy();
                }
            }
        }

        // Verify unified HP pool (no vanilla limb/organ death, no pain/bleeding, dies only when full HP pool hits 0)
        if (!LCEntityHealthUtility.UsesUnifiedHealthPool(bird) || LCEntityHealthUtility.UsesUnifiedHealthPool(worker))
        {
            throw new InvalidOperationException(
                $"UsesUnifiedHealthPool mismatch: bird={LCEntityHealthUtility.UsesUnifiedHealthPool(bird)}, worker={LCEntityHealthUtility.UsesUnifiedHealthPool(worker)}");
        }
        float maxBirdHp = LCEntityHealthUtility.GetMaxHealth(bird);
        BodyPartRecord birdBrain = bird.health.hediffSet.GetBrain() ?? bird.RaceProps.body.corePart;
        DamageInfo brainCut = new DamageInfo(RimWorld.DamageDefOf.Cut, maxBirdHp * 0.50f, armorPenetration: 2f, instigator: worker, hitPart: birdBrain);
        brainCut.SetIgnoreArmor(true);
        bird.TakeDamage(brainCut);
        if (bird.Dead || bird.Downed
            || bird.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart || h.def == RimWorld.HediffDefOf.BloodLoss)
            || bird.health.hediffSet.PainTotal > 0.001f
            || bird.health.hediffSet.BleedRateTotal > 0.001f)
        {
            throw new InvalidOperationException(
                $"Unified HP brain-hit check failed: dead={bird.Dead}, downed={bird.Downed}, pain={bird.health.hediffSet.PainTotal}, bleed={bird.health.hediffSet.BleedRateTotal}");
        }
        float pctAfter50 = bird.health.summaryHealth.SummaryHealthPercent;
        if (Math.Abs(pctAfter50 - 0.50f) > 0.05f)
        {
            throw new InvalidOperationException($"Expected ~0.50 HP after 50% damage hit, got {pctAfter50:0.###} (cur={LCEntityHealthUtility.GetCurrentHealth(bird):0.##}/{maxBirdHp:0.##}).");
        }

        DamageInfo heartCrush = new DamageInfo(RimWorld.DamageDefOf.Blunt, maxBirdHp * 0.40f, armorPenetration: 2f, instigator: worker, hitPart: bird.RaceProps.body.corePart);
        heartCrush.SetIgnoreArmor(true);
        Verse.DamageWorker.DamageResult crushRes = bird.TakeDamage(heartCrush);
        float pctAfter90 = bird.health.summaryHealth.SummaryHealthPercent;
        if (bird.Dead || bird.Downed || crushRes.totalDamageDealt < maxBirdHp * 0.35f || pctAfter90 <= 0.03f || pctAfter90 >= 0.20f)
        {
            throw new InvalidOperationException(
                $"Unified HP 90% damage check failed: dead={bird.Dead}, downed={bird.Downed}, dealt={crushRes.totalDamageDealt:0.##}, pct={pctAfter90:0.###}");
        }

        DamageInfo finishHit = new DamageInfo(RimWorld.DamageDefOf.Cut, maxBirdHp * 0.20f, armorPenetration: 2f, instigator: worker);
        finishHit.SetIgnoreArmor(true);
        bird.TakeDamage(finishHit);
        if (!bird.Dead || bird.health.summaryHealth.SummaryHealthPercent > 0.001f)
        {
            throw new InvalidOperationException($"Unified HP entity did not die after >=100% damage: dead={bird.Dead}, pct={bird.health.summaryHealth.SummaryHealthPercent:0.###}");
        }
        if (bird.Corpse != null && !bird.Corpse.Destroyed)
        {
            bird.Corpse.Destroy();
        }
        foreach (Thing spawnedBirdCleanup in birdPosCleanupList(map, bird.PositionHeld))
        {
            if (!spawnedBirdCleanup.Destroyed) spawnedBirdCleanup.Destroy();
        }

        static List<Thing> birdPosCleanupList(Map m, IntVec3 pos) => pos.InBounds(m)
            ? m.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("PunishingBirdEgg", true))
                .Where(t => t.Position.DistanceTo(pos) <= 3f).ToList()
            : new List<Thing>();

        if (bird.Spawned) bird.DeSpawn();
        if (!bird.Destroyed) bird.Destroy();

        // Also validate Meltdown countdown expiry failure -> Qliphoth drops to 0 & breaches room
        Thing sfMeltEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ScorchedGirlEgg", true));
        if (!terminal2.TryCaptureThing(sfMeltEgg))
        {
            throw new InvalidOperationException("terminal2.TryCaptureThing(ScorchedGirlEgg) failed for meltdown expiry test.");
        }
        Pawn sfMeltPawn = terminal2.HeldPawn ?? throw new InvalidOperationException("ScorchedGirl failed to enter terminal2.");
        CompAbnormality sfMeltAbn = sfMeltPawn.GetComp<CompAbnormality>();
        if (!terminal2.TriggerMeltdown(60) || !terminal2.IsInMeltdown)
        {
            throw new InvalidOperationException("terminal2.TriggerMeltdown(60) failed on ScorchedGirl.");
        }
        for (int mt = 0; mt < 65; mt++)
        {
            Find.TickManager.DoSingleTick();
        }
        if (terminal2.IsInMeltdown || terminal2.Occupied || sfMeltAbn.QliphothCountCurrent != 0 || !sfMeltAbn.Escaped)
        {
            throw new InvalidOperationException(
                $"Meltdown expiry failure did not force breach: isMelting={terminal2.IsInMeltdown}, occupied={terminal2.Occupied}, Q={sfMeltAbn.QliphothCountCurrent}, escaped={sfMeltAbn.Escaped}");
        }
        if (sfMeltPawn.Spawned) sfMeltPawn.DeSpawn();
        if (!sfMeltPawn.Destroyed) sfMeltPawn.Destroy();

        // 3) Reuse terminal2 to test special migrated abnormalities: WellCheers, LittleRed hire/recall, and Portrait tool
        Thing wcEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WellCheersEgg", true));
        if (!terminal2.TryCaptureThing(wcEgg))
        {
            throw new InvalidOperationException("terminal2.TryCaptureThing(WellCheersEgg) returned false.");
        }
        Pawn wcPawn = terminal2.HeldPawn ?? throw new InvalidOperationException("WellCheers failed to enter room containment.");
        CompAbnormality wcComp = wcPawn.GetComp<CompAbnormality>();
        AcceptanceReport canDrink = (AcceptanceReport)wcComp.GetType().GetMethod("CanDirectDrink").Invoke(wcComp, new object[] { worker });
        if (!canDrink.Accepted)
        {
            throw new InvalidOperationException($"Room-contained WellCheers CanDirectDrink failed: {canDrink.Reason}");
        }
        wcComp.GetPlatformGizmos().ToList();
        wcComp.GetPlatformFloatMenuOptions(worker).ToList();
        terminal2.EjectContents();
        if (wcPawn.Spawned) wcPawn.DeSpawn();
        if (!wcPawn.Destroyed) wcPawn.Destroy();

        Thing lrEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LittleRedRidingHoodedMercenaryEgg", true));
        if (!terminal2.TryCaptureThing(lrEgg))
        {
            throw new InvalidOperationException("terminal2.TryCaptureThing(LittleRedEgg) returned false.");
        }
        Pawn lrPawn = terminal2.HeldPawn ?? throw new InvalidOperationException("LittleRed failed to enter room containment.");
        CompAbnormality lrComp = lrPawn.GetComp<CompAbnormality>();
        lrComp.PeBoxComp.CurAmountIndiPebox = 50;
        Pawn lrTarget = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            PawnKindDefOf.Colonist, Faction.OfAncientsHostile, forceGenerateNewPawn: true,
            canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f));
        GenSpawn.Spawn(lrTarget, FindSafePawnCell(map, map.Center), map);
        bool hired = (bool)lrComp.GetType().GetMethod("RequestMercenaryHunt", BindingFlags.Public | BindingFlags.Instance).Invoke(lrComp, new object[] { lrTarget });
        if (!hired || terminal2.Occupied || lrPawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>().CurrentlyContainedInRoom)
        {
            throw new InvalidOperationException("LittleRed RequestMercenaryHunt failed from room containment.");
        }
        bool recalled = (bool)lrComp.GetType().GetMethod("RecallFromHunt").Invoke(lrComp, null);
        if (!recalled || terminal2.HeldPawn != lrPawn || !lrPawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>().CurrentlyContainedInRoom)
        {
            throw new InvalidOperationException("LittleRed RecallFromHunt failed to return into room terminal.");
        }
        if (!lrComp.Props.canWanderInContainmentRoom)
        {
            throw new InvalidOperationException("LittleRed should have canWanderInContainmentRoom=true.");
        }
        terminal2.Notify_StudyStart(worker);
        Job simWaitJob = JobMaker.MakeJob(RimWorld.JobDefOf.Wait, worker);
        simWaitJob.expiryInterval = 90;
        lrPawn.jobs.StartJob(simWaitJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
        lrComp.Notify_Studied(worker);
        if (lrPawn.CurJobDef == RimWorld.JobDefOf.Wait)
        {
            throw new InvalidOperationException("Wandering abnormality remained frozen in Wait job after study finished.");
        }
        terminal2.EjectContents();
        if (lrTarget.Spawned) lrTarget.DeSpawn();
        if (!lrTarget.Destroyed) lrTarget.Destroy();
        if (lrPawn.Spawned) lrPawn.DeSpawn();
        if (!lrPawn.Destroyed) lrPawn.Destroy();

        Thing portraitEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("PortraitOfAnotherWorldEgg", true));
        if (!terminal2.TryCaptureThing(portraitEgg))
        {
            throw new InvalidOperationException("terminal2.TryCaptureThing(PortraitEgg) returned false.");
        }
        Pawn portraitPawn = terminal2.HeldPawn ?? throw new InvalidOperationException("Portrait failed to enter room containment.");
        CompAbnormalityTool portraitTool = terminal2.HeldToolComp ?? throw new InvalidOperationException("Portrait HeldToolComp is null.");
        if (!portraitTool.IsOperational || !portraitPawn.Spawned)
        {
            throw new InvalidOperationException("Room-contained Portrait tool is not operational/spawned.");
        }
        portraitPawn.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, portraitPawn.DrawPos);
        portraitPawn.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, portraitPawn.DrawPos);
        portraitPawn.DynamicDrawPhaseAt(DrawPhase.Draw, portraitPawn.DrawPos);
        terminal2.DrawExtraSelectionOverlays();
        terminal2.EjectContents();
        portraitTool.RevertToEgg(map, terminal2.Position);

        if (terminal2.Spawned) terminal2.Destroy();
        if (nonEmployee.Spawned) nonEmployee.DeSpawn();
        if (!nonEmployee.Destroyed) nonEmployee.Destroy();
        if (worker.Spawned) worker.DeSpawn();
        if (!worker.Destroyed) worker.Destroy();
        Find.TickManager.DoSingleTick();

        Log.Message("[LC17 Validation] GAMEPLAY PASS; RoomContainment; door/role/static_OneSin/study_PeBox/wandering_PunishingBird/WellCheers/LittleRed_recall/Portrait_tool");
    }

    private static void ValidateRoomContainmentAfterLoad(Map map)
    {
        Building_AbnormalityContainmentTerminal savedTerminal = map.listerBuildings
            .AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>()
            .FirstOrDefault(t => t.HeldPawn?.def?.defName == "OneSin"
                && t.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430901);

        if (savedTerminal == null)
        {
            throw new InvalidOperationException("Saved LC_ContainmentTerminal holding OneSin (0x4c430901) missing after reload.");
        }

        Pawn held = savedTerminal.HeldPawn;
        CompAbnormalityHoldingPlatformTarget target = held.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
        if (!held.Spawned || target == null || !target.CurrentlyContainedInRoom || !savedTerminal.IsRoomValidForContainment(out _) || held.GetRoom() != savedTerminal.ContainmentRoom)
        {
            throw new InvalidOperationException("Reloaded room-contained OneSin state or room binding is invalid.");
        }

        if (savedTerminal.ContainmentRoom?.Role?.defName != "LC_ContainmentRoom")
        {
            throw new InvalidOperationException($"Reloaded containment room role mismatch: {savedTerminal.ContainmentRoom?.Role?.defName ?? "<null>"}.");
        }

        if (GenHostility.HostileTo(held, Faction.OfPlayer))
        {
            throw new InvalidOperationException("Reloaded room-contained OneSin became hostile to player after load.");
        }

        savedTerminal.GetGizmos().ToList();
        savedTerminal.GetInspectString();
        savedTerminal.DrawGUIOverlay();
        savedTerminal.DrawExtraSelectionOverlays();
        if (map.GetComponent<MapComponent_LCQliphothMeltdown>() == null)
        {
            throw new InvalidOperationException("MapComponent_LCQliphothMeltdown missing after save/reload.");
        }
        MapComponent_LCExtraction loadedExtract = map.GetComponent<MapComponent_LCExtraction>();
        if (loadedExtract == null || loadedExtract.TotalExtractions < 2)
        {
            throw new InvalidOperationException($"MapComponent_LCExtraction missing or lost TotalExtractions after reload: {loadedExtract?.TotalExtractions ?? -1}");
        }
        Log.Message("[LC17 Validation] GAMEPLAY PASS; RoomContainmentReload; terminal_and_OneSin_persisted");
    }

    private static void ValidateAbnormalityExtraction(Map map)
    {
        if (LCAnomalyCore.Defs.ThingDefOf.LC_ExtractionWell == null)
        {
            throw new InvalidOperationException("ThingDefOf.LC_ExtractionWell is null.");
        }
        if (LCAnomalyCore.Defs.ResearchProjectDefOf.ExtractAbnormalityRiturals == null)
        {
            throw new InvalidOperationException("ResearchProjectDefOf.ExtractAbnormalityRiturals is null.");
        }
        if (LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_Start == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_DropIn == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_Hover == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_Select == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_Confirm == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_Reroll == null
            || LCAnomalyCore.Defs.SoundDefOf.LC_Sound_Extraction_Manifest == null)
        {
            throw new InvalidOperationException("One or more LC_Sound_Extraction_* SoundDefs failed to bind.");
        }
        if (LCAnomalyCore.Defs.SongDefOf.Anomaly_Extract == null)
        {
            throw new InvalidOperationException("SongDefOf.Anomaly_Extract is null.");
        }

        IntVec3 wellCell = FindSafePawnCell(map, map.Center);
        var well = (LCAnomalyCore.Buildings.Building_ExtractionWell)ThingMaker.MakeThing(LCAnomalyCore.Defs.ThingDefOf.LC_ExtractionWell);
        well.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(well, wellCell, map);
        CompPowerTrader wellPower = well.GetComp<CompPowerTrader>();
        if (wellPower != null)
        {
            wellPower.PowerOn = true;
        }
        if (!well.IsOperational)
        {
            throw new InvalidOperationException("LC_ExtractionWell failed to become operational with power on.");
        }
        well.GetGizmos().ToList();
        well.GetInspectString();

        MapComponent_LCExtraction mgr = MapComponent_LCExtraction.GetFor(map)
            ?? throw new InvalidOperationException("MapComponent_LCExtraction.GetFor returned null.");
        if (!mgr.IsFreeCharterAvailable)
        {
            throw new InvalidOperationException("Expected fresh map to have IsFreeCharterAvailable=true.");
        }

        // 1) Free First-Charter Standard 3-choice draw + confirm
        if (!mgr.TryBeginExtraction(LCExtractionTier.Standard, out string err))
        {
            throw new InvalidOperationException($"Free Standard TryBeginExtraction failed: {err}");
        }
        if (!mgr.HasPendingDraw || mgr.PendingCandidates.Count != 3 || mgr.PendingCandidates.Distinct().Count() != 3)
        {
            throw new InvalidOperationException($"Expected 3 distinct candidates, got {mgr.PendingCandidates?.Count ?? 0}.");
        }
        var dialog = new LCAnomalyCore.UI.Dialog_LC_ExtractAbnormality(map, well);
        if (dialog.InitialSize.x < 900f)
        {
            throw new InvalidOperationException("Dialog_LC_ExtractAbnormality InitialSize unexpected.");
        }

        mgr.SelectedIndex = 0;
        if (!mgr.TryConfirmExtraction(well, out Thing freeEgg, out err) || freeEgg == null || !freeEgg.Spawned)
        {
            throw new InvalidOperationException($"TryConfirmExtraction (free) failed: {err}");
        }
        if (mgr.TotalExtractions != 1 || mgr.HasPendingDraw || mgr.CooldownTicksRemaining <= 0)
        {
            throw new InvalidOperationException("Post-confirm extraction state invalid (TotalExtractions/HasPendingDraw/Cooldown).");
        }
        if (freeEgg.Spawned) freeEgg.DeSpawn();
        if (!freeEgg.Destroyed) freeEgg.Destroy();

        // 2) Verify cooldown blocks while active (outside GodMode), then reset cooldown for Abyss test
        bool prevGod = DebugSettings.godMode;
        DebugSettings.godMode = false;
        try
        {
            if (mgr.CanStartExtraction(LCExtractionTier.Standard, out _))
            {
                throw new InvalidOperationException("Extraction should be blocked during cooldown when godMode is off.");
            }
            mgr.DebugResetCooldown();
            if (mgr.CooldownTicksRemaining != 0)
            {
                throw new InvalidOperationException("DebugResetCooldown did not clear cooldown.");
            }

            // 3) Paid Abyss draw + single Reroll + Confirm (guarantee ALEPH present)
            int cogBefore = mgr.CountAvailableResource(LCAnomalyCore.Defs.ThingDefOf.Cogito);
            Thing cogitoStack = ThingMaker.MakeThing(LCAnomalyCore.Defs.ThingDefOf.Cogito);
            cogitoStack.stackCount = 10;
            GenPlace.TryPlaceThing(cogitoStack, wellCell, map, ThingPlaceMode.Near);

            if (!mgr.TryBeginExtraction(LCExtractionTier.Abyss, out err))
            {
                throw new InvalidOperationException($"Paid Abyss TryBeginExtraction failed: {err}");
            }
            int cogAfterBegin = mgr.CountAvailableResource(LCAnomalyCore.Defs.ThingDefOf.Cogito);
            if (cogAfterBegin != cogBefore + 5)
            {
                throw new InvalidOperationException($"Expected {cogBefore + 5} Cogito remaining after Abyss draw, got {cogAfterBegin}.");
            }
            if (!mgr.PendingCandidates.Any(d => LCAnomalyCore.Util.AbnormalityUtility.ResolveCodexEntry(d)?.category?.defName == "ALEPH"))
            {
                throw new InvalidOperationException("Abyss tier extraction did not include an ALEPH candidate.");
            }

            if (!mgr.TryRerollCandidates(out err) || !mgr.UsedReroll)
            {
                throw new InvalidOperationException($"TryRerollCandidates failed: {err}");
            }
            int expectedAfterReroll = cogBefore + (mgr.IsFreeRerollAvailable ? 5 : 4);
            int cogAfterReroll = mgr.CountAvailableResource(LCAnomalyCore.Defs.ThingDefOf.Cogito);
            if (cogAfterReroll != expectedAfterReroll)
            {
                throw new InvalidOperationException($"Expected {expectedAfterReroll} Cogito remaining after Reroll (free={mgr.IsFreeRerollAvailable}), got {cogAfterReroll}.");
            }
            if (mgr.TryRerollCandidates(out _))
            {
                throw new InvalidOperationException("Second reroll in same draw should have been rejected.");
            }

            mgr.SelectedIndex = 0;
            if (!mgr.TryConfirmExtraction(well, out Thing abyssEgg, out err) || abyssEgg == null || !abyssEgg.Spawned)
            {
                throw new InvalidOperationException($"TryConfirmExtraction (Abyss) failed: {err}");
            }
            if (mgr.TotalExtractions != 2)
            {
                throw new InvalidOperationException($"Expected TotalExtractions=2, got {mgr.TotalExtractions}.");
            }
            if (abyssEgg.Spawned) abyssEgg.DeSpawn();
            if (!abyssEgg.Destroyed) abyssEgg.Destroy();

            // 4) Extraction Team Department Core perk: free Re-Extract + 33% cooldown reduction, and Story statistic tracking
            mgr.DebugResetCooldown();
            int cdBeforeCore = mgr.EffectiveCooldownTicks;
            ThingDef extractionCoreDef = DefDatabase<ThingDef>.GetNamed("LC_DepartmentCore_ExtractionTeam", true);
            var extractionCore = (Building_DepartmentCore)ThingMaker.MakeThing(extractionCoreDef);
            extractionCore.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(extractionCore, FindSafePawnCell(map, wellCell), map);
            if (extractionCore.CompPower != null) extractionCore.CompPower.PowerOn = true;
            try
            {
                if (!mgr.IsFreeRerollAvailable
                    || mgr.EffectiveCooldownTicks > cdBeforeCore
                    || mgr.EffectiveCooldownTicks >= MapComponent_LCExtraction.ExtractionCooldownTicks)
                {
                    throw new InvalidOperationException($"ExtractionTeam perk failed to activate (freeReroll={mgr.IsFreeRerollAvailable}, cd={mgr.EffectiveCooldownTicks}, before={cdBeforeCore}).");
                }
                if (!mgr.TryBeginExtraction(LCExtractionTier.Shallow, out err))
                {
                    throw new InvalidOperationException($"Shallow draw with ExtractionTeam failed: {err}");
                }
                int cogBeforeFreeReroll = mgr.CountAvailableResource(LCAnomalyCore.Defs.ThingDefOf.Cogito);
                if (!mgr.TryRerollCandidates(out err))
                {
                    throw new InvalidOperationException($"ExtractionTeam free TryRerollCandidates failed: {err}");
                }
                int cogAfterFreeReroll = mgr.CountAvailableResource(LCAnomalyCore.Defs.ThingDefOf.Cogito);
                if (cogAfterFreeReroll != cogBeforeFreeReroll)
                {
                    throw new InvalidOperationException($"Expected free reroll under ExtractionTeam, but Cogito changed {cogBeforeFreeReroll} -> {cogAfterFreeReroll}.");
                }
                mgr.SelectedIndex = 0;
                if (!mgr.TryConfirmExtraction(well, out Thing perkEgg, out err) || perkEgg == null || !perkEgg.Spawned)
                {
                    throw new InvalidOperationException($"TryConfirmExtraction (ExtractionTeam perk) failed: {err}");
                }
                if (perkEgg.Spawned) perkEgg.DeSpawn();
                if (!perkEgg.Destroyed) perkEgg.Destroy();
            }
            finally
            {
                if (extractionCore.Spawned) extractionCore.DeSpawn();
                if (!extractionCore.Destroyed) extractionCore.Destroy();
            }

            Type storyAccess = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("LCAnomalyStory.StoryComponents", false))
                .FirstOrDefault(t => t != null);
            object dev = storyAccess?.GetProperty("Development", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (dev != null)
            {
                long extractedStat = (long)dev.GetType().GetMethod("GetStatistic", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(dev, new object[] { "abnormalitiesExtracted" });
                if (extractedStat < 3)
                {
                    throw new InvalidOperationException($"LCAnomalyStory did not record abnormalitiesExtracted >= 3 (got {extractedStat}).");
                }
            }

            foreach (Thing leftover in map.listerThings.ThingsOfDef(LCAnomalyCore.Defs.ThingDefOf.Cogito).ToList())
            {
                if (leftover.Spawned) leftover.DeSpawn();
                if (!leftover.Destroyed) leftover.Destroy();
            }
        }
        finally
        {
            DebugSettings.godMode = prevGod;
            mgr.DebugResetCooldown();
            if (well.Spawned) well.DeSpawn();
            if (!well.Destroyed) well.Destroy();
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; AbnormalityExtraction; well/free_charter/cooldown/abyss_aleph/reroll/confirm");
    }
}
