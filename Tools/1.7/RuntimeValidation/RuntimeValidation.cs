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
        if (++frames < 30) return;
        ran = true;
        try
        {
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
            LCAnomalyCore.Singleton.LCCanvasSingleton.Instance.ShowWarningUI(MusicUtils.EWarningLevel.Third);
            for (int i = 0; i < 120; i++) Find.TickManager.DoSingleTick();
            GameDataSaveLoader.SaveGame("LC17_Validation");
            Log.Message("[LC17 Validation] MAP PASS; pawns=" + pawns + "; things=" + things);
        }
        catch (Exception e) { Log.Error("[LC17 Validation] Map test failed: " + e); }
    }
}
