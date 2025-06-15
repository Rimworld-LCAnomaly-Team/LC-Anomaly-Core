using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Comp
{
    public abstract class LC_CompRequireThingSpawner : ThingComp, IThingHolder
    {
        protected int ticksUntilSpawn;

        public virtual LC_CompProperties_RequireThingSpawner PropsSpawner => (LC_CompProperties_RequireThingSpawner)props;

        protected bool PowerOn => parent.GetComp<CompPowerTrader>()?.PowerOn ?? false;

        protected ThingOwner innerContainer;

        public bool HasRequireThingInstalled => innerContainer != null && innerContainer.Count != 0;

        public LC_CompRequireThingSpawner()
        {
            innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad)
            {
                ResetCountdown();
            }
        }

        public override void CompTick()
        {
            if (HasRequireThingInstalled)
                TickInterval(1);
        }

        public override void CompTickRare()
        {
            if (HasRequireThingInstalled)
                TickInterval(250);
        }

        protected virtual void TickInterval(int interval)
        {
            if (!parent.Spawned)
            {
                return;
            }

            CompCanBeDormant comp = parent.GetComp<CompCanBeDormant>();
            if (comp != null)
            {
                if (!comp.Awake)
                {
                    return;
                }
            }
            else if (parent.Position.Fogged(parent.Map))
            {
                return;
            }

            if (!PropsSpawner.requiresPower || PowerOn)
            {
                ticksUntilSpawn -= interval;
                CheckShouldSpawn();
            }
        }

        protected virtual bool TryDoSpawn()
        {
            if (!parent.Spawned)
            {
                return false;
            }

            if (PropsSpawner.spawnMaxAdjacent >= 0)
            {
                int num = 0;
                for (int i = 0; i < 9; i++)
                {
                    IntVec3 c = parent.Position + GenAdj.AdjacentCellsAndInside[i];
                    if (!c.InBounds(parent.Map))
                    {
                        continue;
                    }

                    List<Thing> thingList = c.GetThingList(parent.Map);
                    for (int j = 0; j < thingList.Count; j++)
                    {
                        if (thingList[j].def == PropsSpawner.thingToSpawn)
                        {
                            num += thingList[j].stackCount;
                            if (num >= PropsSpawner.spawnMaxAdjacent)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            if (TryFindSpawnCell(parent, PropsSpawner.thingToSpawn, PropsSpawner.spawnCount, out var result))
            {
                Thing thing = ThingMaker.MakeThing(PropsSpawner.thingToSpawn);
                thing.stackCount = PropsSpawner.spawnCount;
                if (thing == null)
                {
                    Log.Error("Could not spawn anything for " + parent);
                }

                if (PropsSpawner.inheritFaction && thing.Faction != parent.Faction)
                {
                    thing.SetFaction(parent.Faction);
                }

                GenPlace.TryPlaceThing(thing, result, parent.Map, ThingPlaceMode.Direct, out var lastResultingThing);
                if (PropsSpawner.spawnForbidden)
                {
                    lastResultingThing.SetForbidden(value: true);
                }

                if (PropsSpawner.showMessageIfOwned && parent.Faction == Faction.OfPlayer)
                {
                    Messages.Message("MessageCompSpawnerSpawnedItem".Translate(PropsSpawner.thingToSpawn.LabelCap), thing, MessageTypeDefOf.PositiveEvent);
                }

                return true;
            }

            return false;
        }

        protected virtual bool TryFindSpawnCell(Thing parent, ThingDef thingToSpawn, int spawnCount, out IntVec3 result)
        {
            foreach (IntVec3 item in GenAdj.CellsAdjacent8Way(parent).InRandomOrder())
            {
                if (!item.Walkable(parent.Map))
                {
                    continue;
                }

                Verse.Building edifice = item.GetEdifice(parent.Map);
                if ((edifice != null && thingToSpawn.IsEdifice()) || (edifice is Building_Door building_Door && !building_Door.FreePassage) || (parent.def.passability != Traversability.Impassable && !GenSight.LineOfSight(parent.Position, item, parent.Map)))
                {
                    continue;
                }

                bool flag = false;
                List<Thing> thingList = item.GetThingList(parent.Map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing thing = thingList[i];
                    if (thing.def.category == ThingCategory.Item && (thing.def != thingToSpawn || thing.stackCount > thingToSpawn.stackLimit - spawnCount))
                    {
                        flag = true;
                        break;
                    }
                }

                if (!flag)
                {
                    result = item;
                    return true;
                }
            }

            result = IntVec3.Invalid;
            return false;
        }

        protected void CheckShouldSpawn()
        {
            if (ticksUntilSpawn <= 0)
            {
                ResetCountdown();
                TryDoSpawn();
            }
        }

        protected void ResetCountdown()
        {
            ticksUntilSpawn = PropsSpawner.spawnIntervalRange.RandomInRange;
        }

        public override void PostExposeData()
        {
            string text = (PropsSpawner.saveKeysPrefix.NullOrEmpty() ? null : (PropsSpawner.saveKeysPrefix + "_"));
            Scribe_Values.Look(ref ticksUntilSpawn, text + "ticksUntilSpawn", 0);
            Scribe_Deep.Look<ThingOwner>(ref this.innerContainer, "innerContainer", this);
        }

        public override IEnumerable<Verse.Gizmo> CompGetGizmosExtra()
        {
            if (!HasRequireThingInstalled)
            {
                Command_Action command_Action3 = new Command_Action();
                command_Action3.defaultLabel = "InsertPerson".Translate() + "...";
                command_Action3.defaultDesc = "InsertPersonGrowthVatDesc".Translate();
                command_Action3.icon = PropsSpawner.thingRequire.uiIcon;
                command_Action3.action = delegate
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();
                    foreach (var item in parent.Map.listerThings.AllThings.Where(x => x.def == PropsSpawner.thingRequire && x.stackCount == 1))
                    {
                        Thing thing = item;

                        if (true)
                        {
                            list.Add(new FloatMenuOption(thing.LabelCap, delegate
                            {
                                innerContainer.TryAddOrTransfer(thing.SplitOff(1), false);
                            }, thing, Color.white));
                        }
                    }

                    if (!list.Any())
                    {
                        list.Add(new FloatMenuOption("NoViablePawns".Translate(), null));
                    }

                    Find.WindowStack.Add(new FloatMenu(list));
                };

                if (!PowerOn)
                {
                    command_Action3.Disable("NoPower".Translate().CapitalizeFirst());
                }

                yield return command_Action3;
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Spawn " + PropsSpawner.thingToSpawn.label,
                    icon = PropsSpawner.thingToSpawn.uiIcon,
                    action = delegate
                    {
                        ResetCountdown();
                        TryDoSpawn();
                    }
                };

                if (HasRequireThingInstalled)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: ForceDropItem",
                        action = delegate
                        {
                            innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near);
                        }
                    };
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (PropsSpawner.writeTimeLeftToSpawn && (!PropsSpawner.requiresPower || PowerOn))
            {
                if (HasRequireThingInstalled)
                    return "NextSpawnedItemIn".Translate(GenLabel.ThingLabel(PropsSpawner.thingToSpawn, null, PropsSpawner.spawnCount)).Resolve() + ": " + ticksUntilSpawn.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor);
                else
                    return "RequireThingNotInstalledText".Translate() + PropsSpawner.thingRequire.label.Translate();
            }

            return null;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, this.GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return this.innerContainer;
        }
    }
}