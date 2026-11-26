using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompCogitoBucketSpawner</c> 类型。</summary>
    public class CompCogitoBucketSpawner : LC_CompRequireThingSpawner
    {
        /// <inheritdoc />
        public override IEnumerable<Verse.Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: SelfExplode",
                    action = delegate
                    {
                        parent.GetComp<CompExplosive>()?.StartWick();
                    }
                };

                //yield return new Command_Action
                //{
                //    defaultLabel = "DEV: WaringPoints +10",
                //    action = delegate
                //    {
                //        Current.Game.GetComponent<GameComponent_LC>().CurWarningPoints += 10;
                //    }
                //};

                //yield return new Command_Action
                //{
                //    defaultLabel = "DEV: WaringPoints -10",
                //    action = delegate
                //    {
                //        Current.Game.GetComponent<GameComponent_LC>().CurWarningPoints -= 10;
                //    }
                //};

                //yield return new Command_Action
                //{
                //    defaultLabel = "DEV: GetDict",
                //    action = delegate
                //    {
                //        var component = Current.Game.GetComponent<GameComponent_LC>();
                //        if (component.AnomalyStatusSavedDict != null)
                //        {
                //            foreach (var key in component.AnomalyStatusSavedDict.Keys)
                //            {
                //                Log.Message($"key = {key.defName}" +
                //                    $"\nIndiPeBoxAmount = {component.AnomalyStatusSavedDict[key].IndiPeBoxAmount}" +
                //                    $"\nWeaponAmount = {component.AnomalyStatusSavedDict[key].ExtractedEgoWeaponAmount}" +
                //                    $"\nArmorAmount = {component.AnomalyStatusSavedDict[key].ExtractedEgoArmorAmount}");
                //            }
                //        }
                //        else
                //        {
                //            Log.Warning($"dict is null");
                //        }
                //    }
                //};
            }

            if (HasRequireThingInstalled)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "ExtractInstantFromNerveLabel".Translate();
                command_Action.defaultDesc = "ExtractInstantFromNerveDesc".Translate();
                command_Action.icon = PropsSpawner.thingToSpawn.uiIcon;
                command_Action.action = delegate
                {
                    innerContainer.ClearAndDestroyContents();

                    Thing thing = ThingMaker.MakeThing(Defs.ThingDefOf.Cogito);
                    thing.stackCount = 10;
                    GenPlace.TryPlaceThing(thing, parent.PositionHeld, parent.MapHeld, ThingPlaceMode.Near);
                };
                yield return command_Action;
            }

            if (parent != null && parent.Spawned && parent.Faction == Faction.OfPlayer && parent.Map != null)
            {
                bool powered = GameComponent.MapComponent_LCExtraction.IsTerminalOperational(parent as Building);
                var mgr = GameComponent.MapComponent_LCExtraction.GetFor(parent.Map);
                Command_Action extractCmd = new Command_Action
                {
                    defaultLabel = "LC_Extract_GizmoLabel".Translate(),
                    defaultDesc = "LC_Extract_GizmoDesc".Translate(),
                    icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Extraction/Extract_Unit_Chassis", false)
                           ?? Defs.ThingDefOf.Cogito?.uiIcon,
                    action = delegate
                    {
                        if (Find.WindowStack.IsOpen<UI.Dialog_LC_ExtractAbnormality>())
                        {
                            return;
                        }

                        (Defs.SoundDefOf.LC_Sound_Extraction_Start ?? RimWorld.SoundDefOf.Click).PlayOneShotOnCamera();
                        Find.WindowStack.Add(new UI.Dialog_LC_ExtractAbnormality(parent.Map, parent as Building));
                    }
                };
                if (!powered && (mgr == null || !mgr.HasPendingDraw))
                {
                    extractCmd.Disable("LC_Extract_NeedPower".Translate());
                }

                yield return extractCmd;
            }
        }
    }
}