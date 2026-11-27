using System.Reflection;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;

namespace LC17Validation;

[StaticConstructorOnStartup]
public static class Startup
{
    public static bool Enabled => Environment.GetEnvironmentVariable("LC17_RUNTIME_TEST") == "1";
    public static bool IsSeries(Def def) => def.modContentPack != null &&
        (def.modContentPack.PackageId.ToLowerInvariant().StartsWith("darthcy.lc.") ||
         def.modContentPack.PackageId == "lcanomaly.animationstudio");

    static Startup()
    {
        if (!Enabled) return;
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            try
            {
                string profile = Environment.GetEnvironmentVariable("LC17_DLC_PROFILE");
                if (profile != null && ModsConfig.AnomalyActive != (profile == "All"))
                    throw new InvalidOperationException("Unexpected Anomaly state for profile " + profile);
                Log.Message("[LC17 Validation] Profile=" + profile + "; AnomalyActive=" + ModsConfig.AnomalyActive);
                var report = new StringBuilder();
                foreach (string method in new[] { "AppendGeneralLoadErrors", "AppendDefInjectionsLoadErros" })
                    typeof(LanguageReportGenerator).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
                        ?.Invoke(null, new object[] { report });
                File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "translation-errors.txt"), report.ToString());
                foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(IsSeries))
                {
                    foreach (string error in def.ConfigErrors())
                        Log.Error("[LC17 Validation] Config " + def.defName + ": " + error);
                    if (def.race != null && !def.race.Humanlike && !def.race.IsMechanoid && def.race.trainability == null)
                        Log.Error("[LC17 Validation] Missing trainability on " + def.defName);
                }
                Log.Message("[LC17 Validation] Def validation complete.");
                if (Environment.GetEnvironmentVariable("LC17_TEST_MODE") == "startup") return;
                if (Environment.GetEnvironmentVariable("LC17_TEST_MODE") == "load")
                {
                    GameDataSaveLoader.LoadGame("LC17_Validation");
                    return;
                }
                LongEventHandler.QueueLongEvent(() =>
                {
                    Current.ProgramState = ProgramState.Entry;
                    Game.ClearCaches();
                    Current.Game = new Game { InitData = new GameInitData(), Scenario = ScenarioDefOf.Crashlanded.scenario };
                    Find.Scenario.PreConfigure();
                    Current.Game.storyteller = new Storyteller(StorytellerDefOf.Cassandra, DifficultyDefOf.Rough);
                    // A fixed small world avoids unrelated NPC-leader generation failures in the 1.7 beta.
                    var factions = DefDatabase<FactionDef>.AllDefsListForReading
                        .Where(f => f.requiredCountAtGameStart > 0 && (f.isPlayer || f.hidden)).ToList();
                    Current.Game.World = WorldGenerator.GenerateWorld(0.05f, "LC-Standalone-1.7", OverallRainfall.Normal,
                        OverallTemperature.Normal, OverallPopulation.Normal, LandmarkDensity.Normal, factions);
                    Find.GameInitData.ChooseRandomStartingTile();
                    Find.GameInitData.mapSize = 150;
                    if (ModsConfig.IdeologyActive) Find.Scenario.PostIdeoChosen();
                    Find.GameInitData.PrepForMapGen();
                    Find.Scenario.PreMapGenerate();
                }, "Play", "GeneratingMap", true, null);
            }
            catch (Exception e) { Log.Error("[LC17 Validation] Startup failed: " + e); }
        });
    }
}

public class ValidationComponent : GameComponent
{
    private bool ran;
    private int frames;
    public ValidationComponent(Game game) { }

    public override void GameComponentUpdate()
    {
        if (!Startup.Enabled || ran || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            return;
        frames++;
        if (Environment.GetEnvironmentVariable("LC17_TEST_MODE") != "load" && frames == 5)
        {
            LCAnomalyCore.Singleton.LCCanvasSingleton.Instance.ShowWarningUI(MusicUtils.EWarningLevel.First);
        }
        if (frames < 30) return;
        ran = true;
        try
        {
            if (Environment.GetEnvironmentVariable("LC17_TEST_MODE") != "load")
            {
                var overlay = LCAnomalyCore.Singleton.LCCanvasSingleton.Instance;
                if (overlay.ScreenDrawCount <= 0)
                    throw new InvalidOperationException($"WarningOverlay did not render on screen UIRoot_Play (screenDraws={overlay.ScreenDrawCount}, windowDraws={overlay.WindowDrawCount}).");
                Log.Message($"[LC17 Validation] WarningOverlay PASS; screenDraws={overlay.ScreenDrawCount}; windowDraws={overlay.WindowDrawCount}");
            }
            if (Environment.GetEnvironmentVariable("LC17_TEST_MODE") == "load")
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                for (int i = 0; i < 120; i++) Find.TickManager.DoSingleTick();
                GameplayValidation.CheckReload(Find.CurrentMap);
                Log.Message("[LC17 Validation] SAVE LOAD PASS; pawns=" + Find.CurrentMap.mapPawns.AllPawnsSpawned.Count);
                return;
            }
            var map = Find.CurrentMap;
            int pawns = 0, things = 0;
            // Random scenario colonists can have research disabled by their backstory.
            // Generate an eligible fixture instead of assuming the first colonist can do this work.
            Pawn testWorker;
            Rand.PushState(0x4c4317);
            try
            {
                testWorker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false, forceNoBackstory: true,
                    fixedBiologicalAge: 25f, forceBaselinerChance: 1f,
                    validatorPreGear: p => p.skills != null && !p.skills.GetSkill(SkillDefOf.Intellectual).TotallyDisabled));
            }
            finally { Rand.PopState(); }
            IntVec3 spawnCenter = map.Center;
            if (!spawnCenter.Standable(map) || spawnCenter.GetRegion(map) == null)
            {
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 35,
                        c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetRegion(map) != null && c.GetEdifice(map) == null,
                        out spawnCenter))
                {
                    spawnCenter = map.AllCells.First(c => c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && c.GetRegion(map) != null && c.GetEdifice(map) == null);
                }
            }
            GenSpawn.Spawn(testWorker, CellFinder.RandomClosewalkCellNear(spawnCenter, map, 5), map);
            var employee = testWorker.GetComp<CompPawnStatus>();
            if (employee == null) throw new InvalidOperationException("Employee status component is missing.");
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(Startup.IsSeries).ToList())
            {
                try
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(kind, LCContainmentUtility.AbnormalityFaction);
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(spawnCenter, map, 25);
                    GenSpawn.Spawn(pawn, cell, map);
                    pawn.Drawer.renderer.EnsureGraphicsInitialized();
                    pawn.Drawer.renderer.RenderPawnAt(pawn.DrawPos);
                    pawn.GetGizmos().ToList();
                    var abnormality = pawn.GetComp<CompAbnormality>();
                    if (abnormality != null)
                    {
                        if (abnormality.AbnormalityCategory == null || abnormality.HoldingPlaformTargetComp == null ||
                            abnormality.StudiableComp == null)
                            throw new InvalidOperationException("Missing abnormality category or containment/study component.");
                        foreach (EAnomalyWorkType work in Enum.GetValues(typeof(EAnomalyWorkType)))
                        {
                            float rate = abnormality.CheckStudierSkillRequire(employee, work);
                            if (float.IsNaN(rate) || rate < 0f || rate > 1f)
                                throw new InvalidOperationException("Invalid work success rate: " + rate);
                        }
                    }
                    pawns++;
                    Log.Message("[LC17 Validation] Spawned " + kind.defName);
                }
                catch (Exception e) { Log.Error("[LC17 Validation] Pawn " + kind.defName + ": " + e); }
            }
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(Startup.IsSeries)
                .Where(d => (d.category == ThingCategory.Item || d.category == ThingCategory.Building) &&
                    !typeof(Corpse).IsAssignableFrom(d.thingClass)).ToList())
            {
                try
                {
                    ThingDef material = null;
                    if (def.MadeFromStuff)
                        material = GenStuff.AllowedStuffsFor(def).FirstOrDefault();
                    Thing thing = ThingMaker.MakeThing(def, material);
                    thing.GetGizmos().ToList();
                    if (def.graphicData != null) _ = thing.Graphic.MatSingle;
                    things++;
                }
                catch (Exception e) { Log.Error("[LC17 Validation] Thing " + def.defName + ": " + e); }
            }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            GameplayValidation.Run(map, employee);
            ValidateFourColorInfoStats(testWorker);
            LCAnomalyCore.Singleton.LCCanvasSingleton.Instance.ShowWarningUI(MusicUtils.EWarningLevel.Third);
            for (int i = 0; i < 120; i++) Find.TickManager.DoSingleTick();
            GameDataSaveLoader.SaveGame("LC17_Validation");
            Log.Message("[LC17 Validation] MAP PASS; pawns=" + pawns + "; things=" + things);
        }
        catch (Exception e) { Log.Error("[LC17 Validation] Map test failed: " + e); }
    }

    private static void ValidateFourColorInfoStats(Pawn testWorker)
    {
        StatDef riskStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_RiskLevel;
        StatDef typeStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_DamageType;
        StatDef wepDmgStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_WeaponDamage;
        StatDef skillDmgStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_SkillDamage;
        StatDef multStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_DamageMultiplier;
        StatDef redStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistRed;
        StatDef whiteStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistWhite;
        StatDef blackStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistBlack;
        StatDef paleStat = LCAnomalyCore.Defs.StatDefOf.LC_FourColor_ResistPale;

        foreach (string channel in new[] { "Red", "White", "Black", "Pale" })
        {
            if (ContentFinder<UnityEngine.Texture2D>.Get($"Fleck/FourColor/LC_DamageIcon_{channel}", reportFailure: false) == null)
                throw new InvalidOperationException($"Missing four-color icon texture Fleck/FourColor/LC_DamageIcon_{channel}.");
        }

        var allEgoWeapons = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.IsWeapon && d.defName.StartsWith("EgoWeapon_", StringComparison.Ordinal))
            .ToList();
        if (allEgoWeapons.Count < 22)
            throw new InvalidOperationException($"Expected at least 22 EgoWeapon_* defs, found {allEgoWeapons.Count}.");

        foreach (ThingDef wDef in allEgoWeapons)
        {
            Thing w = ThingMaker.MakeThing(wDef);
            try
            {
                StatRequest wReq = StatRequest.For(w);
                if (!wepDmgStat.Worker.ShouldShowFor(wReq) || w.GetStatValue(wepDmgStat) <= 0f)
                    throw new InvalidOperationException($"{wDef.defName} missing or zero LC_FourColor_WeaponDamage.");
                if (!skillDmgStat.Worker.ShouldShowFor(wReq) || w.GetStatValue(skillDmgStat) <= 0f)
                    throw new InvalidOperationException($"{wDef.defName} missing or zero LC_FourColor_SkillDamage.");

                string wepLabel = wepDmgStat.Worker.GetStatDrawEntryLabel(wepDmgStat, w.GetStatValue(wepDmgStat), ToStringNumberSense.Absolute, wReq);
                string skillLabel = skillDmgStat.Worker.GetStatDrawEntryLabel(skillDmgStat, w.GetStatValue(skillDmgStat), ToStringNumberSense.Absolute, wReq);
                string skillExp = skillDmgStat.Worker.GetExplanationFull(wReq, ToStringNumberSense.Absolute, w.GetStatValue(skillDmgStat));
                if (string.IsNullOrEmpty(wepLabel) || string.IsNullOrEmpty(skillLabel) || string.IsNullOrEmpty(skillExp))
                    throw new InvalidOperationException($"{wDef.defName} returned empty weapon/skill damage label or explanation.");
            }
            finally
            {
                w.Destroy();
            }
        }

        void AssertWeapon(string defName, float expectedType, float expectedRisk, string expectedWepSubstr, string expectedSkillSubstr)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: true);
            Thing weapon = ThingMaker.MakeThing(def);
            try
            {
                StatRequest req = StatRequest.For(weapon);
                if (!riskStat.Worker.ShouldShowFor(req)
                    || !typeStat.Worker.ShouldShowFor(req)
                    || !wepDmgStat.Worker.ShouldShowFor(req)
                    || !skillDmgStat.Worker.ShouldShowFor(req)
                    || !multStat.Worker.ShouldShowFor(req))
                    throw new InvalidOperationException($"Weapon {defName} missing dedicated four-color stats.");
                if (redStat.Worker.ShouldShowFor(req))
                    throw new InvalidOperationException($"Weapon {defName} should not show armor resistance stats.");

                float actualRisk = weapon.GetStatValue(riskStat);
                float actualType = weapon.GetStatValue(typeStat);
                float actualMult = weapon.GetStatValue(multStat);
                if (Math.Abs(actualRisk - expectedRisk) > 0.01f || Math.Abs(actualType - expectedType) > 0.01f || actualMult <= 0f)
                    throw new InvalidOperationException($"Weapon {defName} stat mismatch: risk={actualRisk}, type={actualType}, mult={actualMult}");

                string typeLabel = typeStat.Worker.GetStatDrawEntryLabel(typeStat, actualType, ToStringNumberSense.Absolute, req);
                string riskLabel = riskStat.Worker.GetStatDrawEntryLabel(riskStat, actualRisk, ToStringNumberSense.Absolute, req);
                string wepDmgLabel = wepDmgStat.Worker.GetStatDrawEntryLabel(wepDmgStat, weapon.GetStatValue(wepDmgStat), ToStringNumberSense.Absolute, req);
                string skillDmgLabel = skillDmgStat.Worker.GetStatDrawEntryLabel(skillDmgStat, weapon.GetStatValue(skillDmgStat), ToStringNumberSense.Absolute, req);
                string explanation = typeStat.Worker.GetExplanationFull(req, ToStringNumberSense.Absolute, actualType);
                if (string.IsNullOrEmpty(typeLabel) || string.IsNullOrEmpty(riskLabel) || string.IsNullOrEmpty(explanation))
                    throw new InvalidOperationException($"Weapon {defName} returned empty label/explanation.");
                if (!wepDmgLabel.Contains(expectedWepSubstr))
                    throw new InvalidOperationException($"Weapon {defName} damage label '{wepDmgLabel}' expected to contain '{expectedWepSubstr}'.");
                if (!skillDmgLabel.Contains(expectedSkillSubstr))
                    throw new InvalidOperationException($"Weapon {defName} skill damage label '{skillDmgLabel}' expected to contain '{expectedSkillSubstr}'.");

                if (defName == "EgoWeapon_FuneralOfDeadButterflies")
                {
                    string whiteText = "LC_DamageType_White".Translate();
                    string blackText = "LC_DamageType_Black".Translate();
                    if (!typeLabel.Contains(whiteText) || !typeLabel.Contains(blackText))
                        throw new InvalidOperationException($"Solemn Lament should display both White and Black damage types, got '{typeLabel}'.");
                }
                else if (defName == "EgoWeapon_JudgementBird")
                {
                    string skillExp = skillDmgStat.Worker.GetExplanationFull(req, ToStringNumberSense.Absolute, weapon.GetStatValue(skillDmgStat));
                    string expectedThrust = "LC_SkillStage_Thrust".Translate();
                    if (skillExp.Contains("StormTick") || !skillExp.Contains(expectedThrust))
                        throw new InvalidOperationException($"Justitia skill stage labels were not properly localized. Exp='{skillExp}'.");
                }
            }
            finally
            {
                weapon.Destroy();
            }
        }

        AssertWeapon("EgoWeapon_NothingThere", (float)LCAnomalyCore.Damage.FourColorDamageType.Red, (float)LCAnomalyCore.Damage.LCRiskLevel.ALEPH, "28~36", "65~95");
        AssertWeapon("EgoWeapon_OneSin", (float)LCAnomalyCore.Damage.FourColorDamageType.White, (float)LCAnomalyCore.Damage.LCRiskLevel.ZAYIN, "3~6", "24~32");
        AssertWeapon("EgoWeapon_FuneralOfDeadButterflies", (float)LCAnomalyCore.Damage.FourColorDamageType.White, (float)LCAnomalyCore.Damage.LCRiskLevel.WAW, "8 × 2", "60~84");
        AssertWeapon("EgoWeapon_JudgementBird", (float)LCAnomalyCore.Damage.FourColorDamageType.Pale, (float)LCAnomalyCore.Damage.LCRiskLevel.ALEPH, "14%", "60.6%");
        AssertWeapon("EgoWeapon_Laetitia", (float)LCAnomalyCore.Damage.FourColorDamageType.Black, (float)LCAnomalyCore.Damage.LCRiskLevel.HE, "15", "32~43");

        void AssertArmor(string defName, float expectedRisk, float expectedR, float expectedW, float expectedB, float expectedP)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: true);
            Thing armor = ThingMaker.MakeThing(def);
            try
            {
                StatRequest req = StatRequest.For(armor);
                if (!riskStat.Worker.ShouldShowFor(req)
                    || !redStat.Worker.ShouldShowFor(req)
                    || !whiteStat.Worker.ShouldShowFor(req)
                    || !blackStat.Worker.ShouldShowFor(req)
                    || !paleStat.Worker.ShouldShowFor(req))
                    throw new InvalidOperationException($"Armor {defName} missing dedicated four-color resistance/risk stats.");
                if (typeStat.Worker.ShouldShowFor(req)
                    || wepDmgStat.Worker.ShouldShowFor(req)
                    || skillDmgStat.Worker.ShouldShowFor(req))
                    throw new InvalidOperationException($"Armor {defName} should not show weapon damage/skill stats.");

                float actualRisk = armor.GetStatValue(riskStat);
                float r = armor.GetStatValue(redStat);
                float w = armor.GetStatValue(whiteStat);
                float b = armor.GetStatValue(blackStat);
                float p = armor.GetStatValue(paleStat);
                if (Math.Abs(actualRisk - expectedRisk) > 0.01f
                    || Math.Abs(r - expectedR) > 0.01f
                    || Math.Abs(w - expectedW) > 0.01f
                    || Math.Abs(b - expectedB) > 0.01f
                    || Math.Abs(p - expectedP) > 0.01f)
                {
                    throw new InvalidOperationException($"Armor {defName} stat mismatch: risk={actualRisk}, R={r}, W={w}, B={b}, P={p}");
                }

                string redLabel = redStat.Worker.GetStatDrawEntryLabel(redStat, r, ToStringNumberSense.Absolute, req);
                string explanation = redStat.Worker.GetExplanationFull(req, ToStringNumberSense.Absolute, r);
                if (string.IsNullOrEmpty(redLabel) || string.IsNullOrEmpty(explanation))
                    throw new InvalidOperationException($"Armor {defName} returned empty label/explanation.");
            }
            finally
            {
                armor.Destroy();
            }
        }

        AssertArmor("Apparel_NothingThereSuit", (float)LCAnomalyCore.Damage.LCRiskLevel.ALEPH, 0.2f, 0.5f, 0.5f, 1.0f);
        AssertArmor("Apparel_OneSinSuit", (float)LCAnomalyCore.Damage.LCRiskLevel.ZAYIN, 0.9f, 0.8f, 0.9f, 2.0f);

        ThingDef shirtDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_BasicShirt");
        if (shirtDef != null && redStat.Worker.ShouldShowFor(StatRequest.For(shirtDef, GenStuff.DefaultStuffFor(shirtDef))))
            throw new InvalidOperationException("Vanilla non-armor Apparel_BasicShirt should not show four-color armor resistances.");

        ThingDef revolverDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Revolver");
        if (revolverDef != null && typeStat.Worker.ShouldShowFor(StatRequest.For(revolverDef, null)))
            throw new InvalidOperationException("Vanilla Gun_Revolver should not show four-color weapon damage type.");

        Apparel ntSuit = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_NothingThereSuit", errorOnFail: true)) as Apparel;
        if (ntSuit != null && testWorker.apparel != null)
        {
            testWorker.apparel.Wear(ntSuit, dropReplacedApparel: false);
            try
            {
                float pawnRisk = testWorker.GetStatValue(riskStat);
                float pawnRed = testWorker.GetStatValue(redStat);
                if (Math.Abs(pawnRisk - (float)LCAnomalyCore.Damage.LCRiskLevel.ALEPH) > 0.01f || pawnRed > 0.25f)
                    throw new InvalidOperationException($"Pawn wearing Mimicry expected ALEPH & R<=0.25, got risk={pawnRisk}, R={pawnRed}.");
            }
            finally
            {
                testWorker.apparel.Remove(ntSuit);
                ntSuit.Destroy();
            }
        }

        // Verify Abnormality-side risk-level suppression (both defense & innate attack) + info panel text
        Map map = Find.CurrentMap;
        Pawn nothingThere = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.def.defName == "NothingThere" && !p.Dead);
        bool spawnedTempNt = false;
        if (nothingThere == null)
        {
            nothingThere = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("NothingThere", errorOnFail: true), LCContainmentUtility.AbnormalityFaction);
            GenSpawn.Spawn(nothingThere, testWorker.Position, map);
            spawnedTempNt = true;
        }

        Pawn oneSin = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.def.defName == "OneSin" && !p.Dead);
        bool spawnedTempOs = false;
        if (oneSin == null)
        {
            oneSin = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("OneSin", errorOnFail: true), LCContainmentUtility.AbnormalityFaction);
            GenSpawn.Spawn(oneSin, testWorker.Position, map);
            spawnedTempOs = true;
        }

        try
        {
            ThingDef oneSinWep = DefDatabase<ThingDef>.GetNamed("EgoWeapon_OneSin", errorOnFail: true);
            ThingDef ntWep = DefDatabase<ThingDef>.GetNamed("EgoWeapon_NothingThere", errorOnFail: true);
            DamageDef whiteDmgDef = LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage;
            DamageDef redDmgDef = LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;

            float zayinVsAleph = DamageUtils.GetAdjustedDamage(
                new DamageInfo(whiteDmgDef, 100f, weapon: oneSinWep),
                nothingThere,
                LCAnomalyCore.Damage.FourColorDamageType.White,
                LCAnomalyCore.Damage.LCRiskLevel.ZAYIN);
            float alephVsAleph = DamageUtils.GetAdjustedDamage(
                new DamageInfo(whiteDmgDef, 100f, weapon: ntWep),
                nothingThere,
                LCAnomalyCore.Damage.FourColorDamageType.White,
                LCAnomalyCore.Damage.LCRiskLevel.ALEPH);
            float defenseRatio = zayinVsAleph / Math.Max(0.001f, alephVsAleph);
            if (Math.Abs(defenseRatio - 0.4f) > 0.02f)
                throw new InvalidOperationException($"Expected ZAYIN vs ALEPH Abnormality ratio 0.4, got {defenseRatio:0.###} ({zayinVsAleph}/{alephVsAleph}).");

            float alephVsZayin = DamageUtils.GetAdjustedDamage(
                new DamageInfo(redDmgDef, 100f, weapon: ntWep),
                oneSin,
                LCAnomalyCore.Damage.FourColorDamageType.Red,
                LCAnomalyCore.Damage.LCRiskLevel.ALEPH);
            float zayinVsZayin = DamageUtils.GetAdjustedDamage(
                new DamageInfo(redDmgDef, 100f, weapon: oneSinWep),
                oneSin,
                LCAnomalyCore.Damage.FourColorDamageType.Red,
                LCAnomalyCore.Damage.LCRiskLevel.ZAYIN);
            float offenseRatio = alephVsZayin / Math.Max(0.001f, zayinVsZayin);
            if (Math.Abs(offenseRatio - 2.0f) > 0.02f)
                throw new InvalidOperationException($"Expected ALEPH vs ZAYIN Abnormality ratio 2.0, got {offenseRatio:0.###} ({alephVsZayin}/{zayinVsZayin}).");

            float ntVsUnarmored = DamageUtils.GetAdjustedDamage(
                new DamageInfo(redDmgDef, 100f, instigator: nothingThere),
                testWorker,
                LCAnomalyCore.Damage.FourColorDamageType.Red,
                LCAnomalyCore.Damage.LCRiskLevel.ZAYIN);

            Apparel ntArmorCheck = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_NothingThereSuit", errorOnFail: true)) as Apparel;
            float ntVsArmored = 0f;
            if (ntArmorCheck != null && testWorker.apparel != null)
            {
                testWorker.apparel.Wear(ntArmorCheck, dropReplacedApparel: false);
                try
                {
                    ntVsArmored = DamageUtils.GetAdjustedDamage(
                        new DamageInfo(redDmgDef, 100f, instigator: nothingThere),
                        testWorker,
                        LCAnomalyCore.Damage.FourColorDamageType.Red,
                        LCAnomalyCore.Damage.LCRiskLevel.ZAYIN);
                }
                finally
                {
                    testWorker.apparel.Remove(ntArmorCheck);
                    ntArmorCheck.Destroy();
                }
            }
            float innateRatio = ntVsUnarmored / Math.Max(0.001f, ntVsArmored / 0.2f);
            if (Math.Abs(innateRatio - 2.0f) > 0.05f)
                throw new InvalidOperationException($"Expected NothingThere innate ALEPH attack ratio 2.0 vs ZAYIN, got {innateRatio:0.###} ({ntVsUnarmored} vs {ntVsArmored}).");

            StatRequest ntReq = StatRequest.For(nothingThere);
            string ntRedExp = redStat.Worker.GetExplanationFull(ntReq, ToStringNumberSense.Absolute, nothingThere.GetStatValue(redStat));
            string ntRiskExp = riskStat.Worker.GetExplanationFull(ntReq, ToStringNumberSense.Absolute, nothingThere.GetStatValue(riskStat));
            if (!ntRedExp.Contains("ALEPH") || ntRedExp.Contains("ZAYIN") || !ntRiskExp.Contains("ALEPH") || ntRiskExp.Contains("ZAYIN"))
                throw new InvalidOperationException($"NothingThere info card should show ALEPH without ZAYIN armor line. redExp='{ntRedExp}', riskExp='{ntRiskExp}'.");

            ValidateHitAndDodgeRules(testWorker, nothingThere, wepDmgStat);
        }
        finally
        {
            if (spawnedTempNt && !nothingThere.Destroyed) nothingThere.Destroy();
            if (spawnedTempOs && !oneSin.Destroyed) oneSin.Destroy();
        }

        Log.Message("[LC17 Validation] FourColorInfoStats PASS");
    }

    private static void ValidateHitAndDodgeRules(Pawn testWorker, Pawn nothingThere, StatDef wepDmgStat)
    {
        var settings = LCAnomalyCore.Settings.Setting_LCAnomalyCore_Main.Settings;
        bool prevGuaranteed = settings != null && settings.If_GuaranteedFourColorHit;

        MethodInfo getDodgeMethod = typeof(Verb_MeleeAttack).GetMethod("GetDodgeChance", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo getNonMissMethod = typeof(Verb_MeleeAttack).GetMethod("GetNonMissChance", BindingFlags.Instance | BindingFlags.NonPublic);
        if (getDodgeMethod == null || getNonMissMethod == null)
            throw new InvalidOperationException("Failed to reflect Verb_MeleeAttack.GetDodgeChance / GetNonMissChance.");

        ThingDef magicBulletDef = DefDatabase<ThingDef>.GetNamed("EgoWeapon_DerFreischutz", errorOnFail: true);
        ThingDef laetitiaDef = DefDatabase<ThingDef>.GetNamed("EgoWeapon_Laetitia", errorOnFail: true);
        ThingDef oneSinWepDef = DefDatabase<ThingDef>.GetNamed("EgoWeapon_OneSin", errorOnFail: true);

        Thing magicBullet = ThingMaker.MakeThing(magicBulletDef);
        Thing laetitia = ThingMaker.MakeThing(laetitiaDef);
        Thing oneSinWep = ThingMaker.MakeThing(oneSinWepDef);
        Pawn shooter = (testWorker != null && testWorker.Spawned && !testWorker.Dead)
            ? testWorker
            : Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?.FirstOrDefault(p => !p.Dead);
        bool spawnedTempShooter = false;
        if (shooter == null)
        {
            shooter = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            GenSpawn.Spawn(shooter, nothingThere.Position, nothingThere.Map);
            spawnedTempShooter = true;
        }
        try
        {
            if (settings != null) settings.If_GuaranteedFourColorHit = false;

            // 1. Magic Bullet always guarantees hit + shows tooltip note
            Verb mbVerb = magicBullet.TryGetComp<CompEquippable>()?.PrimaryVerb;
            if (mbVerb == null)
                throw new InvalidOperationException("Magic Bullet missing PrimaryVerb.");
            mbVerb.caster = shooter;
            ShotReport mbReport = ShotReport.HitReportFor(shooter, mbVerb, new LocalTargetInfo(nothingThere));
            if (mbReport.TotalEstimatedHitChance < 0.999f || mbReport.PassCoverChance < 0.999f)
                throw new InvalidOperationException($"Magic Bullet expected guaranteed hit (1.0), got Total={mbReport.TotalEstimatedHitChance}, Cover={mbReport.PassCoverChance}.");

            StatRequest mbReq = StatRequest.For(magicBullet);
            string mbWepExp = wepDmgStat.Worker.GetExplanationFull(mbReq, ToStringNumberSense.Absolute, magicBullet.GetStatValue(wepDmgStat));
            string expectedGuaranteedNote = "LC_Stat_WeaponGuaranteedHit".Translate();
            if (!mbWepExp.Contains(expectedGuaranteedNote))
                throw new InvalidOperationException($"Magic Bullet explanation missing guaranteed-hit note '{expectedGuaranteedNote}'.");

            // 2. Large Abnormality (NothingThere BodySize=2.4) has 0% melee dodge in default mode
            Verb oneSinMelee = oneSinWep.TryGetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault(v => v is Verb_MeleeAttack);
            if (oneSinMelee == null)
                throw new InvalidOperationException("EgoWeapon_OneSin missing Verb_MeleeAttack.");
            oneSinMelee.caster = shooter;
            float ntDodgeDefault = (float)getDodgeMethod.Invoke(oneSinMelee, new object[] { new LocalTargetInfo(nothingThere) });
            if (ntDodgeDefault > 0.0001f)
                throw new InvalidOperationException($"NothingThere (BodySize={nothingThere.BodySize}) expected 0% melee dodge, got {ntDodgeDefault}.");

            // 3. Optional guaranteed-hit toggle makes normal E.G.O weapons (ranged + melee) always hit & bypass dodge
            if (settings != null) settings.If_GuaranteedFourColorHit = true;

            Verb laetVerb = laetitia.TryGetComp<CompEquippable>()?.PrimaryVerb;
            if (laetVerb == null)
                throw new InvalidOperationException("EgoWeapon_Laetitia missing PrimaryVerb.");
            laetVerb.caster = shooter;
            ShotReport laetReport = ShotReport.HitReportFor(shooter, laetVerb, new LocalTargetInfo(nothingThere));
            if (laetReport.TotalEstimatedHitChance < 0.999f || laetReport.PassCoverChance < 0.999f)
                throw new InvalidOperationException($"Guaranteed-hit mode expected 1.0 hit chance on Laetitia, got {laetReport.TotalEstimatedHitChance}.");

            float nonMiss = (float)getNonMissMethod.Invoke(oneSinMelee, new object[] { new LocalTargetInfo(shooter) });
            float dodge = (float)getDodgeMethod.Invoke(oneSinMelee, new object[] { new LocalTargetInfo(shooter) });
            if (nonMiss < 0.999f || dodge > 0.0001f)
                throw new InvalidOperationException($"Guaranteed-hit mode expected melee nonMiss=1 & dodge=0, got nonMiss={nonMiss}, dodge={dodge}.");
        }
        finally
        {
            if (settings != null) settings.If_GuaranteedFourColorHit = prevGuaranteed;
            if (spawnedTempShooter && !shooter.Destroyed) shooter.Destroy();
            if (!magicBullet.Destroyed) magicBullet.Destroy();
            if (!laetitia.Destroyed) laetitia.Destroy();
            if (!oneSinWep.Destroyed) oneSinWep.Destroy();
        }
    }
}
