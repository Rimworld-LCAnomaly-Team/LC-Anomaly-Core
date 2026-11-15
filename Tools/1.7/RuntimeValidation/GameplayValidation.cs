using System.Reflection;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Defs;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LC17Validation;

public static class GameplayValidation
{
    public static void Run(Map map, CompPawnStatus employee)
    {
        Pawn worker = (Pawn)employee.parent;
        worker.skills.GetSkill(SkillDefOf.Intellectual).Level = 20;
        int index = 0;
        foreach (string name in new[] { "OneSin", "Laetitia", "MeatLantern", "BurrowingHeaven" })
        {
            var platform = (Building_AbnormalityHoldingPlatform)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LC_HoldingPlatform"));
            platform.SetFaction(Faction.OfPlayer);
            IntVec3 cell = map.AllCells.First(c => CellRect.CenteredOn(c, 2).Cells.All(p =>
                p.InBounds(map) && p.Walkable(map) && p.GetEdifice(map) == null && p.GetFirstPawn(map) == null));
            GenSpawn.Spawn(platform, cell, map);
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
            platform.Notify_StudyStart(worker);
            int workTicks = name == "MeatLantern" ? 2400 : 1;
            for (int i = 0; i < workTicks; i++) platform.Notify_Studying(worker);
            for (int i = 0; i < 24; i++) platform.Notify_StudyInterval(employee);
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
        var spawnEffect = (LC_FX_Standard)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("SpawningLaetitiaMonster"), map.Center, map);
        spawnEffect.GetType().GetMethod("Init").Invoke(spawnEffect, null);
        int beforeMonsters = map.mapPawns.AllPawnsSpawned.Count(p => p.def.defName == "LaetitiaMonster");
        spawnEffect.Complete();
        Pawn monster = map.mapPawns.AllPawnsSpawned.Last(p => p.def.defName == "LaetitiaMonster");
        if (map.mapPawns.AllPawnsSpawned.Count(p => p.def.defName == "LaetitiaMonster") != beforeMonsters + 1 ||
            monster.Faction != LCContainmentUtility.AbnormalityFaction)
            throw new InvalidOperationException("Monster spawning failed.");
        Pawn target = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        GenSpawn.Spawn(target, CellFinder.RandomClosewalkCellNear(map.Center, map, 3), map);
        float healthBefore = target.health.summaryHealth.SummaryHealthPercent;
        monster.GetType().GetMethod("AttackDirect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(monster, new object[] { target });
        if (target.health.summaryHealth.SummaryHealthPercent >= healthBefore)
            throw new InvalidOperationException("Monster attack caused no damage.");
        Log.Message("[LC17 Validation] GAMEPLAY PASS; LaetitiaMonster; spawn/faction/ability/damage/effects");
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
    }
}
