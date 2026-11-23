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
    private static IntVec3 FindSafePlatformCell(Map map)
    {
        return map.AllCells.First(c =>
            c.x >= 8 && c.z >= 8 && c.x < map.Size.x - 8 && c.z < map.Size.z - 8
            && CellRect.CenteredOn(c, 2).Cells.All(p =>
                p.InBounds(map) && !p.Fogged(map) && p.Standable(map) && p.GetRegion(map) != null
                && p.GetEdifice(map) == null && p.GetFirstPawn(map) == null));
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
        Pawn worker = (Pawn)employee.parent;
        worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
        int index = 0;
        foreach (string name in new[] { "OneSin", "Laetitia", "MeatLantern", "BurrowingHeaven" })
        {
            var platform = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform"));
            platform.SetFaction(Faction.OfPlayer);
            IntVec3 cell = FindSafePlatformCell(map);
            GenSpawn.Spawn(platform, cell, map);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg"));
            int signature = 0x4c430010 + index++;
            AbnormalityUtility.TransferSignature(egg, signature);
            platform.innerContainer.TryAdd(egg);
            egg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(platform.innerContainer);
            Pawn held = platform.HeldPawn ?? throw new InvalidOperationException("Egg failed to enter containment: " + name);
            var abnormality = held.GetComp<CompAbnormality>();
            if (abnormality.biosignature != signature) throw new InvalidOperationException("Identity did not survive containment: " + name);
            AbnormalityUtility.Discover(held.def);
            var entry = DefDatabase<AbnormalityCodexEntryDef>.GetNamedSilentFail(held.def.defName);
            if (entry != null && !entry.Discovered) throw new InvalidOperationException("Codex discovery failed: " + name);
            platform.GetGizmos().ToList();
            held.Drawer.renderer.EnsureGraphicsInitialized();
            held.Drawer.renderer.RenderPawnAt(platform.DrawPos);
            if (name == "BurrowingHeaven")
            {
                CompAbnormalityHoldingPlatformTarget bhTarget = held.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
                if (bhTarget == null || bhTarget.Props.heldDrawScale >= 1f || bhTarget.Props.heldDrawScale <= 0f)
                    throw new InvalidOperationException("BurrowingHeaven expected heldDrawScale < 1 inside holding platform.");
                platform.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, platform.DrawPos);
                platform.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, platform.DrawPos);
                platform.DynamicDrawPhaseAt(DrawPhase.Draw, platform.DrawPos);
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

        // P1: SpecialDisplayStats InfoCard entries (apparel, weapon, pawn)
        string expectedArmorLabel = "LC_Stat_FourColorArmor".Translate().CapitalizeFirst();
        if (!powerArmor.SpecialDisplayStats(StatRequest.For(powerArmor, null)).Any(e => e.LabelCap == expectedArmorLabel))
            throw new InvalidOperationException("Apparel_PowerArmor SpecialDisplayStats missing four-color armor StatDrawEntry.");

        ThingDef egoOneSinWeapon = DefDatabase<ThingDef>.GetNamed("EgoWeapon_OneSin", true);
        string expectedWeaponLabel = "LC_Stat_FourColorWeapon".Translate().CapitalizeFirst();
        if (!egoOneSinWeapon.SpecialDisplayStats(StatRequest.For(egoOneSinWeapon, null)).Any(e => e.LabelCap == expectedWeaponLabel))
            throw new InvalidOperationException("EgoWeapon_OneSin SpecialDisplayStats missing four-color weapon StatDrawEntry.");

        Pawn victim = (!target.Destroyed && !target.Dead)
            ? target
            : (map.mapPawns.FreeColonistsSpawned.FirstOrDefault() ?? monster);
        string expectedPawnLabel = "LC_Stat_FourColorPawn".Translate().CapitalizeFirst();
        if (!victim.SpecialDisplayStats().Any(e => e.LabelCap == expectedPawnLabel))
            throw new InvalidOperationException($"Pawn {victim.def.defName} SpecialDisplayStats missing four-color pawn StatDrawEntry.");

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

        Log.Message("[LC17 Validation] GAMEPLAY PASS; P0/P1; codex/eligibility/fourcolor/infocard/feedback");

        ValidateP2BatchA(map, monster);
    }

    private static (Building_AbnormalityHoldingPlatform platform, Pawn held, CompAbnormalityTool tool) ContainToolOnPlatform(
        Map map,
        Pawn testColonist,
        string toolName,
        int signature)
    {
        Building_AbnormalityHoldingPlatform platform =
            (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        platform.SetFaction(Faction.OfPlayer);
        IntVec3 cell = FindSafePlatformCell(map);
        GenSpawn.Spawn(platform, cell, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(toolName + "Egg", true));
        AbnormalityUtility.TransferSignature(egg, signature);
        platform.innerContainer.TryAdd(egg);
        egg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(platform.innerContainer);

        Pawn held = platform.HeldPawn
            ?? throw new InvalidOperationException($"Tool egg failed to materialize on holding platform: {toolName}");
        if (!platform.IsHoldingTool)
            throw new InvalidOperationException($"Holding platform did not recognize held tool abnormality: {toolName}");

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
        held.Drawer.renderer.RenderPawnAt(platform.DrawPos);

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
            throw new InvalidOperationException("PortraitOfAnotherWorld did not revert to matching biosignature egg after leaving platform.");

        // Contain a second bound Portrait on a holding platform to verify Scribe_References survival across save/reload
        const int savedPortraitSig = 0x4c430082;
        Pawn savedWard = map.mapPawns.FreeColonistsSpawned.First(p => !p.Dead && !p.Destroyed);
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

        // Eject Flesh Idol from platform -> verifies peaceful reversion to Egg
        fleshPlatform.EjectContents();
        for (int i = 0; i < 3; i++) Find.TickManager.DoSingleTick();
        if (!heldFlesh.Destroyed && heldFlesh.Spawned)
            throw new InvalidOperationException("FleshIdol did not despawn after leaving platform.");
        if (!map.listerThings.ThingsOfDef(fleshEggDef).Any(t => t.TryGetComp<LC_CompEgg>()?.biosignature == fleshSig))
            throw new InvalidOperationException("FleshIdol did not revert to egg with matching biosignature.");

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
        if (!ironTool.TryDrawHeldOverride(DrawPhase.Draw, ironPlatform.DrawPos))
            throw new InvalidOperationException("WeCanChangeAnything failed to activate bloody override graphic draw while in use.");

        LC_EntityBasePawn heldIronEntity = (LC_EntityBasePawn)heldIron;
        for (int t = 0; t < 720; t++)
        {
            heldIronEntity.TickHolded();
        }

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

        // 3. Old Belief and Promise (T-09-97, ZAYIN SingleUse Tool + Weapon Blessing on Holding Platform)
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
        var (_, _, promiseTool) = ContainToolOnPlatform(map, blessedWielder, "OldBeliefAndPromise", promiseSig);
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
        var bhPlatform = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform"));
        bhPlatform.SetFaction(Faction.OfPlayer);
        IntVec3 bhCell = FindSafePlatformCell(map);
        GenSpawn.Spawn(bhPlatform, bhCell, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        Thing bhEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("BurrowingHeavenEgg"));
        AbnormalityUtility.TransferSignature(bhEgg, bhTrophySig);
        bhPlatform.innerContainer.TryAdd(bhEgg);
        bhEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(bhPlatform.innerContainer);
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

        // 1. Contain NothingThereEgg and verify platform/render/biosignature
        const int ntSig = 0x4c430090;
        var ntPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        ntPlat.SetFaction(Faction.OfPlayer);
        IntVec3 ntCell = FindSafePlatformCell(map);
        GenSpawn.Spawn(ntPlat, ntCell, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        Thing ntEgg = ThingMaker.MakeThing(ntEggDef);
        AbnormalityUtility.TransferSignature(ntEgg, ntSig);
        ntPlat.innerContainer.TryAdd(ntEgg);
        ntEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(ntPlat.innerContainer);

        Pawn heldNt = ntPlat.HeldPawn ?? throw new InvalidOperationException("NothingThereEgg failed to materialize in holding platform.");
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

        // 5a. Wiki Rule II test on temporary undisguised platform: Justice < IV breaches immediately WITHOUT killing worker (Dog form East & West)
        Thing rule2Egg = ThingMaker.MakeThing(ntEggDef);
        var rule2Plat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        rule2Plat.SetFaction(Faction.OfPlayer);
        IntVec3 rule2Cell = FindSafePlatformCell(map);
        GenSpawn.Spawn(rule2Plat, rule2Cell, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        rule2Plat.innerContainer.TryAdd(rule2Egg);
        rule2Egg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(rule2Plat.innerContainer);
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
        if (!rule2Nt.Destroyed && rule2Nt.Spawned) { rule2Nt.DeSpawn(); rule2Nt.Destroy(); }
        if (!rule2Plat.Destroyed && rule2Plat.Spawned) { rule2Plat.DeSpawn(); rule2Plat.Destroy(); }
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

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
        ntPlat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, ntPlat.DrawPos);
        ntPlat.DynamicDrawPhaseAt(DrawPhase.Draw, ntPlat.DrawPos);

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

        // 6. Leave one contained NothingThere platform + healthy Mimicry veteran for Save/Reload validation
        const int ntSavedSig = 0x4c430091;
        var ntSavedPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        ntSavedPlat.SetFaction(Faction.OfPlayer);
        IntVec3 ntSavedCell = FindSafePlatformCell(map);
        GenSpawn.Spawn(ntSavedPlat, ntSavedCell, map);
        map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        Thing ntSavedEgg = ThingMaker.MakeThing(ntEggDef);
        AbnormalityUtility.TransferSignature(ntSavedEgg, ntSavedSig);
        ntSavedPlat.innerContainer.TryAdd(ntSavedEgg);
        ntSavedEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(ntSavedPlat.innerContainer);
        if (ntSavedPlat.HeldPawn?.def?.defName != "NothingThere")
            throw new InvalidOperationException("Saved NothingThere egg failed to enter containment.");

        var leftoverInjuries = new List<Hediff_Injury>();
        veteran.health.hediffSet.GetHediffs(ref leftoverInjuries);
        foreach (Hediff_Injury inj in leftoverInjuries)
            inj.Heal(1000f);

        Log.Message("[LC17 Validation] GAMEPLAY PASS; NothingThere; aleph/work/ego/lifesteal/3forms/red_immune/egg_return");

        ValidatePhase1And2(map, abnSettings);
        ValidatePhase3(map, abnSettings);
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
        foreach (Building_AbnormalityHoldingPlatform plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
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
                    foundSavedBoundPortrait = true;
                    break;
                }
            }
        }
        if (!foundSavedBoundPortrait)
            throw new InvalidOperationException("Bound PortraitOfAnotherWorld on HoldingPlatform did not survive save/load.");

        bool foundSavedEnclosedIron = false;
        foreach (Building_AbnormalityHoldingPlatform plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
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
            throw new InvalidOperationException("Enclosed occupant inside WeCanChangeAnything on HoldingPlatform did not survive save/load.");

        bool foundSavedTrophyPlatform = false;
        foreach (Building_AbnormalityHoldingPlatform plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
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
        foreach (Building_AbnormalityHoldingPlatform plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
        {
            if (plat.HeldPawn?.def?.defName == "NothingThere" && plat.HeldPawn.GetComp<CompAbnormality>()?.biosignature == 0x4c430091)
            {
                plat.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, plat.DrawPos);
                plat.DynamicDrawPhaseAt(DrawPhase.Draw, plat.DrawPos);
                foundSavedNtPlatform = true;
                break;
            }
        }
        if (!foundSavedNtPlatform)
            throw new InvalidOperationException("Saved NothingThere holding platform did not survive save/load.");

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

        ValidatePhase1And2AfterLoad(map);
        ValidatePhase3AfterLoad(map);
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

            var plat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
            plat.SetFaction(Faction.OfPlayer);
            IntVec3 cell = FindSafePlatformCell(map);
            GenSpawn.Spawn(plat, cell, map);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg", true));
            int sig = 0x4c430100 + pIndex++;
            AbnormalityUtility.TransferSignature(egg, sig);
            plat.innerContainer.TryAdd(egg);
            egg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(plat.innerContainer);

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
            plat.Destroy();
        }

        // 3. Unique Mechanics Verification
        worker = SpawnValidationColonist(map, map.Center);
        // 3a. FairyFestival care + jealousy devour on studying another abnormality
        var fairyPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        fairyPlat.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(fairyPlat, FindSafePlatformCell(map), map);
        Thing fEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("FairyFestivalEgg", true));
        fairyPlat.innerContainer.TryAdd(fEgg);
        fEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(fairyPlat.innerContainer);

        var otherPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        otherPlat.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(otherPlat, FindSafePlatformCell(map), map);
        Thing wEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("WellCheersEgg", true));
        otherPlat.innerContainer.TryAdd(wEgg);
        wEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(otherPlat.innerContainer);

        Pawn betrayer = SpawnValidationColonist(map, fairyPlat.Position);
        fairyPlat.HeldPawn.GetComp<CompAbnormality>().Notify_Studied(betrayer);
        HediffDef fairyCareDef = DefDatabase<HediffDef>.GetNamed("LC_FairyCare", true);
        if (!betrayer.health.hediffSet.HasHediff(fairyCareDef))
            throw new InvalidOperationException("FairyFestival did not apply LC_FairyCare after study.");
        otherPlat.Notify_StudyStart(betrayer);
        if (!betrayer.Dead)
            throw new InvalidOperationException("LC_FairyCare failed to devour worker who started studying another Abnormality.");
        fairyPlat.Destroy();

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
        var derPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        derPlat.SetFaction(Faction.OfPlayer);
        IntVec3 cornerCell = FindSafePlatformCell(map);
        GenSpawn.Spawn(derPlat, cornerCell, map);
        Thing dEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("DerFreischutzEgg", true));
        derPlat.innerContainer.TryAdd(dEgg);
        dEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(derPlat.innerContainer);
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
        derPlat.Destroy();

        // 3e. HeartOfAspiration equip -> combat -> safe return AND second active equipper saved across save/load
        var (_, _, heartTool1) = ContainToolOnPlatform(map, worker, "HeartOfAspiration", 0x4c430201);
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

        Pawn savedHeartBearer = SpawnValidationColonist(map, FindFarthestSafeCell(map, minColonistDist: 10));
        if (savedHeartBearer.drafter != null) savedHeartBearer.drafter.Drafted = true;
        var (_, _, savedHeartTool) = ContainToolOnPlatform(map, savedHeartBearer, "HeartOfAspiration", 0x4c430202);
        savedHeartTool.OnEquipped(savedHeartBearer);
        savedHeartTool.GetType().GetMethod("NotifyCombatDamageDealt", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(savedHeartTool, new object[] { savedHeartBearer, worker, 10f });

        // 3f. SnowQueen freeze captive into IThingHolder across save/load
        Pawn savedCaptive = SpawnValidationColonist(map, map.Center);
        var sqPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        sqPlat.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(sqPlat, FindSafePlatformCell(map), map);
        Thing sqEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SnowQueenEgg", true));
        AbnormalityUtility.TransferSignature(sqEgg, 0x4c430203);
        sqPlat.innerContainer.TryAdd(sqEgg);
        sqEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(sqPlat.innerContainer);
        CompAbnormality sqAbn = sqPlat.HeldPawn.GetComp<CompAbnormality>();
        sqAbn.GetType().GetMethod("FreezeCaptive", BindingFlags.Public | BindingFlags.Instance)
            .Invoke(sqAbn, new object[] { savedCaptive });
        if (!(bool)sqAbn.GetType().GetProperty("HasFrozenCaptive", BindingFlags.Public | BindingFlags.Instance).GetValue(sqAbn)
            || savedCaptive.Spawned)
        {
            throw new InvalidOperationException("SnowQueen FreezeCaptive did not seal colonist into frozenContainer.");
        }

        // 3g. NotesFromCrazedResearcher study tracking, safe return, AND damage explosion
        var (_, _, noteTool) = ContainToolOnPlatform(map, worker, "NotesFromCrazedResearcher", 0x4c430204);
        Pawn noteScholar = SpawnValidationColonist(map, map.Center);
        noteTool.OnEquipped(noteScholar);
        HediffDef noteBuffDef = DefDatabase<HediffDef>.GetNamed("LC_ResearcherNoteBuff", true);
        if (!noteScholar.health.hediffSet.HasHediff(noteBuffDef))
            throw new InvalidOperationException("NotesFromCrazedResearcher did not grant LC_ResearcherNoteBuff.");
        sqAbn.Notify_Studied(noteScholar);
        noteTool.OnUnequipped(noteScholar, 1800, returnedNormally: true);
        if (noteScholar.Dead || noteTool.IsEquipped)
            throw new InvalidOperationException("NotesFromCrazedResearcher safe return failed.");

        Pawn noteVictim = SpawnValidationColonist(map, FindFarthestSafeCell(map, minColonistDist: 16));
        var (_, _, noteToolBoom) = ContainToolOnPlatform(map, noteVictim, "NotesFromCrazedResearcher", 0x4c430205);
        noteToolBoom.OnEquipped(noteVictim);
        noteVictim.TakeDamage(new DamageInfo(redDef, 105f, instigator: worker));
        if (!noteVictim.Dead)
            throw new InvalidOperationException("NotesFromCrazedResearcher >=100 damage threshold did not detonate wearer.");

        // Ensure save/reload fixture colonists start healthy and away from roaming test hostiles
        GameComponent_LC gcNow = GameComponent_LC.Instance;
        Pawn savedBlessed = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.equipment?.Primary != null && gcNow != null && gcNow.IsWeaponBlessed(p.equipment.Primary))
            ?? throw new InvalidOperationException("No live colonist holds blessed weapon before save.");
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

        foreach (Building_AbnormalityHoldingPlatform plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
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

                foundSavedSnowQueen = true;
            }
        }

        if (!foundSavedHeart)
            throw new InvalidOperationException("Saved HeartOfAspiration holding platform missing after reload.");
        if (!foundSavedSnowQueen)
            throw new InvalidOperationException("Saved SnowQueen holding platform missing after reload.");
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

            var plat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
            plat.SetFaction(Faction.OfPlayer);
            IntVec3 cell = FindSafePlatformCell(map);
            GenSpawn.Spawn(plat, cell, map);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

            Thing egg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name + "Egg", true));
            int sig = 0x4c430300 + pIndex++;
            AbnormalityUtility.TransferSignature(egg, sig);
            plat.innerContainer.TryAdd(egg);
            egg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(plat.innerContainer);

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
            plat.Destroy();
            if (worker.Spawned) worker.DeSpawn();
            if (!worker.Destroyed) worker.Destroy();
        }

        // 3a. BigBird death-counter drop via Patch_PawnKill hook + Enchantment Snap-Out gizmo
        var bbPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        bbPlat.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(bbPlat, FindSafePlatformCell(map), map);
        Thing bbEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("BigBirdEgg", true));
        bbPlat.innerContainer.TryAdd(bbEgg);
        bbEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(bbPlat.innerContainer);
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
        bbPlat.Destroy();

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

        // 3b. LittleRed mercenary hire -> allied faction + recall -> return to homePlatform + escape counter drop
        var lrPlat = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform", true));
        lrPlat.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(lrPlat, FindSafePlatformCell(map), map);
        Thing lrEgg = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LittleRedRidingHoodedMercenaryEgg", true));
        lrPlat.innerContainer.TryAdd(lrEgg);
        lrEgg.TryGetComp<CompAbnormalityHoldingPlatformTarget>().Notify_HeldOnPlatform(lrPlat.innerContainer);
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
        lrPlat.Destroy();

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

        // Cleanup temporary Phase 3 test pawns & platform
        foreach (Pawn tmp in new[] { tempWorker, lv1User, lv3User })
        {
            if (tmp.Spawned) tmp.DeSpawn();
            if (!tmp.Destroyed) tmp.Destroy();
        }
        bcPlat.Destroy();

        // 3d. Leave one BackwardClock charged to 3/4 on a holding platform for Save/Reload persistence check
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

        Log.Message("[LC17 Validation] GAMEPLAY PASS; Phase3; 6_abnormalities/pale_justitia/bigbird_deaths/littlered_hire/backward_clock/schadenfreude");
        ValidateOrdealAndDepartmentExpansionPhase1(map);
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
                GenSpawn.Spawn(core, FindSafePlatformCell(map), map);
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
                GenSpawn.Spawn(core, FindSafePlatformCell(map), map);
                if (core.CompPower != null) core.CompPower.PowerOn = true;
                cores.Add(core);
                return core;
            }

            Building_DepartmentCore central = SpawnCore("LC_DepartmentCore_CentralCommandTeam");
            Building_DepartmentCore welfare = SpawnCore("LC_DepartmentCore_WelfareTeam");
            Building_DepartmentCore disc = SpawnCore("LC_DepartmentCore_DisciplinaryTeam");

            if (ManagerBulletUtility.GetMaxBulletCharges(map) != 10)
                throw new InvalidOperationException($"Expected 10 max bullet charges with Central+Welfare+Disciplinary, got {ManagerBulletUtility.GetMaxBulletCharges(map)}.");

            GameComponent_LC lc = GameComponent_LC.Instance ?? throw new InvalidOperationException("GameComponent_LC.Instance is null.");
            lc.RefillManagerBullets(map);
            if (lc.GetManagerBulletCharges(map) != 10)
                throw new InvalidOperationException("RefillManagerBullets failed to restore charges to 10.");

            if (central.GetGizmos().Count() < 6 || !welfare.GetGizmos().Any() || !disc.GetGizmos().Any())
                throw new InvalidOperationException("Department cores failed to expose Manager Bullet gizmos.");

            friendly = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
                canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
            GenSpawn.Spawn(friendly, FindSafePawnCell(map, map.Center), map);

            // HP Bullet
            friendly.TakeDamage(new DamageInfo(RimWorld.DamageDefOf.Cut, 12f));
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

            // Execution Bullet (despawn friendly first so execTarget is the sole humanlike target at execCell)
            if (friendly.Spawned) friendly.DeSpawn();
            execTarget = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
                canGeneratePawnRelations: false, forceNoBackstory: true, fixedBiologicalAge: 25f, forceBaselinerChance: 1f));
            IntVec3 execCell = FindSafePawnCell(map, FindFarthestSafeCell(map));
            GenSpawn.Spawn(execTarget, execCell, map);
            if (!ManagerBulletUtility.FireBulletAt(map, execTarget.Position, EManagerBulletType.Execution))
                throw new InvalidOperationException("Execution Bullet failed to fire.");
            if (!execTarget.Destroyed || execCell.GetThingList(map).Any(t => t is Corpse))
                throw new InvalidOperationException("Execution Bullet did not cleanly erase target without leaving a corpse.");

            if (lc.GetManagerBulletCharges(map) != 5)
                throw new InvalidOperationException($"Expected 5 remaining bullet charges after 5 shots, got {lc.GetManagerBulletCharges(map)}.");
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
        Type vfxUtilType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Vfx.OrdealVfxUtility")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Vfx.OrdealVfxUtility type.");
        Type vfxCompType = GenTypes.GetTypeInAnyAssembly("LCAnomalyOrdeals.Vfx.MapComponent_OrdealVfx")
            ?? throw new InvalidOperationException("Missing LCAnomalyOrdeals.Vfx.MapComponent_OrdealVfx type.");

        string[] all22Variants =
        {
            "AmberDawn", "CrimsonDawn", "GreenDawn", "VioletDawn",
            "CrimsonNoon", "GreenNoon", "VioletNoon", "IndigoNoon",
            "AmberDusk", "CrimsonDusk", "GreenDusk",
            "AmberMidnight", "GreenMidnight",
            "VioletMidnightRed", "VioletMidnightWhite", "VioletMidnightBlack", "VioletMidnightPale",
            "WhiteFixerRed", "WhiteFixerWhite", "WhiteFixerBlack", "WhiteFixerPale", "WhiteClaw"
        };
        string[] poseSuffixes = { "_move1", "_move2", "_attack", "_special" };

        foreach (string variant in all22Variants)
        {
            PawnKindDef pk = DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_" + variant, true);
            if (pk.race.race.renderTree != treeDef)
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} does not use renderTree LCOrdeal_Animated.");
            if (pk.race.comps == null || !pk.race.comps.Any(c => c.compClass == compAnimType))
                throw new InvalidOperationException($"Ordeal race {pk.race.defName} missing CompProperties_OrdealAnimator.");

            foreach (string suffix in poseSuffixes)
            {
                string texBase = "Things/Pawn/Ordeals/" + variant + suffix;
                if (ContentFinder<Texture2D>.Get(texBase + "_east", reportFailure: false) == null
                    || ContentFinder<Texture2D>.Get(texBase + "_south", reportFailure: false) == null)
                {
                    throw new InvalidOperationException($"Missing ordeal animation frame texture for {texBase}.");
                }
            }
        }

        string[] vfxTextures =
        {
            "FX/Ordeals/LaserBeam", "FX/Ordeals/LaserCore", "FX/Ordeals/LaserHit", "FX/Ordeals/SparkMidnight",
            "FX/Ordeals/BossPortal", "FX/Ordeals/HollowRing", "FX/Ordeals/SoftGlow", "FX/Ordeals/CraterDecal",
            "FX/Ordeals/SlashArc", "FX/Ordeals/TargetMark",
            "FX/Ordeals/TentacleRed", "FX/Ordeals/TentacleWhite", "FX/Ordeals/TentacleBlack", "FX/Ordeals/PaleEye"
        };
        foreach (string vfxTex in vfxTextures)
        {
            if (ContentFinder<Texture2D>.Get(vfxTex, reportFailure: false) == null)
                throw new InvalidOperationException($"Missing ordeal VFX texture: {vfxTex}");
        }

        Pawn testClaw = null;
        Pawn testTower = null;
        try
        {
            IntVec3 cell1 = FindSafePawnCell(map, FindFarthestSafeCell(map));
            IntVec3 cell2 = FindSafePawnCell(map, cell1);
            testClaw = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_WhiteClaw", true), Faction.OfMechanoids);
            testTower = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LCOrdeal_GreenMidnight", true), Faction.OfMechanoids);
            GenSpawn.Spawn(testClaw, cell1, map);
            GenSpawn.Spawn(testTower, cell2, map);

            MethodInfo mTrigAtk = compAnimType.GetMethod("TriggerAttackOn", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Missing CompOrdealAnimator.TriggerAttackOn.");
            MethodInfo mTrigSpec = compAnimType.GetMethod("TriggerSpecialOn", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Missing CompOrdealAnimator.TriggerSpecialOn.");

            mTrigAtk.Invoke(null, new object[] { testClaw, 40 });
            mTrigSpec.Invoke(null, new object[] { testTower, 40, (Color?)Color.green, 0.9f });

            testClaw.Drawer.renderer.EnsureGraphicsInitialized();
            testClaw.Drawer.renderer.RenderPawnAt(testClaw.DrawPos);
            testTower.Drawer.renderer.EnsureGraphicsInitialized();
            testTower.Drawer.renderer.RenderPawnAt(testTower.DrawPos);

            vfxUtilType.GetMethod("SpawnBeam", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testTower.DrawPos, 45f, 24f, 1.8f, Color.green, 30, true });
            vfxUtilType.GetMethod("SpawnShockwave", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 0.5f, 4f, Color.cyan, 30, 90f });
            vfxUtilType.GetMethod("SpawnPortal", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 3f, Color.magenta, 30, 160f });
            vfxUtilType.GetMethod("SpawnStrikeSprite", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, "FX/Ordeals/PaleEye", 2f, 4f, Color.white, 30, 0.8f, 0f, 0f, false });
            vfxUtilType.GetMethod("SpawnPawnAura", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw, "FX/Ordeals/TargetMark", 1.8f, Color.blue, 30, 90f, 0.25f, true });
            vfxUtilType.GetMethod("SpawnFogLine", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, testTower.DrawPos, 2f, 30 });
            vfxUtilType.GetMethod("SpawnFanSweep", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { map, testClaw.DrawPos, 0f, 240f, 6f, 2f, Color.red, 30 });

            MapComponent vfxComp = map.GetComponent(vfxCompType)
                ?? throw new InvalidOperationException("MapComponent_OrdealVfx not attached to map.");
            vfxComp.MapComponentUpdate();
            int activeCount = (int)vfxCompType.GetProperty("ActiveCount", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vfxComp);
            if (activeCount < 7)
                throw new InvalidOperationException($"Expected >= 7 active ordeal VFX instances, got {activeCount}.");
        }
        finally
        {
            if (testClaw != null && !testClaw.Destroyed) { if (testClaw.Spawned) testClaw.DeSpawn(); testClaw.Destroy(); }
            if (testTower != null && !testTower.Destroyed) { if (testTower.Spawned) testTower.DeSpawn(); testTower.Destroy(); }
        }

        Log.Message("[LC17 Validation] GAMEPLAY PASS; OrdealAnimAndVfx; 22_ordeals_4poses/render_tree/14_vfx_textures/live_vfx_draw");
    }

    private static void ValidatePhase3AfterLoad(Map map)
    {
        bool foundSavedBackwardClock = false;
        foreach (Building_AbnormalityHoldingPlatform plat in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
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
            throw new InvalidOperationException("Saved BackwardClock holding platform missing after reload.");

        if (GameComponent_LC.Instance?.HasCoreSuppression(GameComponent_LC.CoreSuppression_Malkuth) != true
            || GameComponent_LC.Instance?.HasCoreSuppression(GameComponent_LC.CoreSuppression_Binah) != true)
        {
            throw new InvalidOperationException("Core Suppression mastery flags did not survive save/load.");
        }
    }
}
