using LCAnomalyLibrary.Comp;
using LCAnomalyLibrary.GameComponent;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    public class CompCogitoBucketSpawner : LC_CompRequireThingSpawner
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
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
        }
    }
}