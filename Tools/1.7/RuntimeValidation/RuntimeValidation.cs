using System.Reflection;
using System.Text;
using RimWorld;
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
                if (Environment.GetEnvironmentVariable("LC17_TEST_MODE") == "load")
                {
                    GameDataSaveLoader.LoadGame("LC17_Validation");
                    return;
                }
                LongEventHandler.QueueLongEvent(() =>
                {
                    Root_Play.SetupForQuickTestPlay();
                    Find.GameInitData.mapSize = 100;
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
                Log.Message("[LC17 Validation] SAVE LOAD PASS; pawns=" + Find.CurrentMap.mapPawns.AllPawnsSpawned.Count);
                return;
            }
            var map = Find.CurrentMap;
            int pawns = 0, things = 0;
            var employee = map.mapPawns.FreeColonists.First().GetComp<CompPawnStatus>();
            if (employee == null) throw new InvalidOperationException("Employee status component is missing.");
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(Startup.IsSeries).ToList())
            {
                try
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfEntities);
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 25);
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
            LCAnomalyCore.Singleton.LCCanvasSingleton.Instance.ShowWarningUI(MusicUtils.EWarningLevel.Third);
            for (int i = 0; i < 120; i++) Find.TickManager.DoSingleTick();
            GameDataSaveLoader.SaveGame("LC17_Validation");
            Log.Message("[LC17 Validation] MAP PASS; pawns=" + pawns + "; things=" + things);
        }
        catch (Exception e) { Log.Error("[LC17 Validation] Map test failed: " + e); }
    }
}
