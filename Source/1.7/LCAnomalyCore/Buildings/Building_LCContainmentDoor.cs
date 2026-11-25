using System.Collections.Generic;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Patch;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Buildings
{
    /// <summary>
    /// 脑叶公司收容室专用门：
    /// 1) 始终禁止处于房间收容态的异想体开启；
    /// 2) 开启员工门禁限制且邻接已收容异想体的收容室时，仅放行已激活员工（或已处于室内欲离开者、征召/强制/维修救援人员）。
    /// </summary>
    public class Building_LCContainmentDoor : Building_Door
    {
        /// <summary>是否在收容室占用期间限制非员工殖民者穿行。</summary>
        public bool restrictToEmployees = true;

        private bool TryGetOccupiedContainmentRoom(out Room occupiedRoom)
        {
            if (Spawned && Map?.listerThings != null && Map.regionGrid != null)
            {
                ThingDef terminalDef = Defs.ThingDefOf.LC_ContainmentTerminal;
                List<Thing> terminals = terminalDef != null ? Map.listerThings.ThingsOfDef(terminalDef) : null;
                if (terminals != null && terminals.Count > 0)
                {
                    for (int i = 0; i < terminals.Count; i++)
                    {
                        if (terminals[i] is Building_AbnormalityContainmentTerminal term && term.Spawned && term.Occupied)
                        {
                            Room termRoom = Map.regionGrid.GetValidRegionAt_NoRebuild(term.Position)?.Room;
                            if (termRoom == null || termRoom.TouchesMapEdge)
                            {
                                continue;
                            }

                            for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                            {
                                IntVec3 adj = Position + GenAdj.CardinalDirections[d];
                                if (adj.InBounds(Map) && Map.regionGrid.GetValidRegionAt_NoRebuild(adj)?.Room == termRoom)
                                {
                                    occupiedRoom = termRoom;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            occupiedRoom = null;
            return false;
        }

        /// <inheritdoc />
        public override bool PawnCanOpen(Pawn p)
        {
            if (!base.PawnCanOpen(p))
            {
                return false;
            }

            if (Patch_RoomContainment.IsContainedInRoom(p))
            {
                return false;
            }

            if (!restrictToEmployees || !Spawned || Map == null)
            {
                return true;
            }

            if (!TryGetOccupiedContainmentRoom(out Room containmentRoom))
            {
                return true;
            }

            if (p.Faction == Faction.OfPlayer)
            {
                Room pawnRoom = (p.Spawned && p.Map == Map && Map.regionGrid != null)
                    ? Map.regionGrid.GetValidRegionAt_NoRebuild(p.Position)?.Room
                    : null;
                if (p.Drafted || pawnRoom == containmentRoom)
                {
                    return true;
                }

                if (p.GetComp<CompPawnStatus>()?.Enabled == true)
                {
                    return true;
                }

                if (p.CurJob != null && (p.CurJob.playerForced
                    || p.CurJob.def == RimWorld.JobDefOf.Repair
                    || p.CurJob.def == RimWorld.JobDefOf.FinishFrame
                    || p.CurJob.def == RimWorld.JobDefOf.Rescue
                    || p.CurJob.def == RimWorld.JobDefOf.TendPatient))
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        /// <inheritdoc />
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Toggle
            {
                defaultLabel = "LC_ContainmentDoor_RestrictLabel".Translate(),
                defaultDesc = "LC_ContainmentDoor_RestrictDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/WorkableUI", reportFailure: false) ?? BaseContent.BadTex,
                isActive = () => restrictToEmployees,
                toggleAction = () => restrictToEmployees = !restrictToEmployees
            };
        }

        /// <inheritdoc />
        public override string GetInspectString()
        {
            string baseStr = base.GetInspectString();
            string modeStr = (restrictToEmployees
                ? "LC_ContainmentDoor_InspectRestricted"
                : "LC_ContainmentDoor_InspectOpen").Translate();

            return baseStr.NullOrEmpty() ? modeStr : $"{baseStr}\n{modeStr}";
        }

        /// <inheritdoc />
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref restrictToEmployees, "restrictToEmployees", defaultValue: true);
        }
    }
}
