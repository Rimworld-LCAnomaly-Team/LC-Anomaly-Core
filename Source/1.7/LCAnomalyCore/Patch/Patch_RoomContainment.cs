using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using RimWorld;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 房间活体收容补丁：
    /// 1) 处于房间收容态的异想体对外视为非敌对、非威胁，允许员工推门工作而不被炮塔锁敌或触发恐慌逃跑；
    /// 2) 接管房间收容态异想体的任务派发，限制其仅在所属收容室内漫步或定点待机，绝不砸门而出。
    /// </summary>
    public static class Patch_RoomContainment
    {
        /// <summary>判断目标当前是否正处于房间收容控制终端的安全收容中。</summary>
        public static bool IsContainedInRoom(Thing thing)
        {
            return thing is Pawn p
                && !p.Dead
                && p.Spawned
                && p.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.CurrentlyContainedInRoom == true;
        }

        /// <summary>判断目标是否为已突破收容（处于出逃态且未重新被收容）的异想体。</summary>
        public static bool IsBreachedAbnormality(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || IsContainedInRoom(p))
            {
                return false;
            }

            if (p.TryGetComp<CompAbnormality>()?.Escaped == true)
            {
                return true;
            }

            return p.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.isEscaping == true;
        }

        /// <summary>判断指定门当前是否因邻接正在进行工作的收容室而处于锁死状态。</summary>
        /// <remarks>只读访问 Map.listerThings 与 regionGrid，保证寻路后台线程安全。</remarks>
        public static bool IsDoorLockedByActiveWork(Building_Door door)
        {
            if (door == null || !door.Spawned || door.Map?.listerThings == null || door.Map.regionGrid == null)
            {
                return false;
            }

            ThingDef terminalDef = Defs.ThingDefOf.LC_ContainmentTerminal;
            List<Thing> terminals = terminalDef != null ? door.Map.listerThings.ThingsOfDef(terminalDef) : null;
            if (terminals == null || terminals.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < terminals.Count; i++)
            {
                if (terminals[i] is Building_AbnormalityContainmentTerminal term
                    && term.Spawned
                    && term.Occupied
                    && term.IsStudySessionActive)
                {
                    Room termRoom = door.Map.regionGrid.GetValidRegionAt_NoRebuild(term.Position)?.Room;
                    if (termRoom == null || termRoom.TouchesMapEdge)
                    {
                        continue;
                    }

                    for (int d = 0; d < GenAdj.CardinalDirections.Length; d++)
                    {
                        IntVec3 adj = door.Position + GenAdj.CardinalDirections[d];
                        if (adj.InBounds(door.Map) && door.Map.regionGrid.GetValidRegionAt_NoRebuild(adj)?.Room == termRoom)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.CanOpenAnyDoor), MethodType.Getter)]
    public static class Patch_Pawn_CanOpenAnyDoor_RoomContainment
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && Patch_RoomContainment.IsBreachedAbnormality(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    public static class Patch_Building_Door_PawnCanOpen_RoomContainment
    {
        public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
        {
            if (Patch_RoomContainment.IsContainedInRoom(p))
            {
                __result = false;
                return;
            }

            if (Patch_RoomContainment.IsBreachedAbnormality(p))
            {
                __result = true;
                return;
            }

            if (__result && Patch_RoomContainment.IsDoorLockedByActiveWork(__instance))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.FreePassage), MethodType.Getter)]
    public static class Patch_Building_Door_FreePassage_RoomContainment
    {
        public static void Postfix(Building_Door __instance, ref bool __result)
        {
            if (__result && Patch_RoomContainment.IsDoorLockedByActiveWork(__instance))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), new[] { typeof(Thing), typeof(Thing) })]
    public static class Patch_GenHostility_ThingThing_RoomContainment
    {
        public static void Postfix(Thing a, Thing b, ref bool __result)
        {
            if (__result && (Patch_RoomContainment.IsContainedInRoom(a) || Patch_RoomContainment.IsContainedInRoom(b)))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), new[] { typeof(Thing), typeof(Faction) })]
    public static class Patch_GenHostility_ThingFaction_RoomContainment
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (__result && Patch_RoomContainment.IsContainedInRoom(t))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ThreatDisabled))]
    public static class Patch_Pawn_ThreatDisabled_RoomContainment
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (!__result && Patch_RoomContainment.IsContainedInRoom(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Patch_JobTracker_DetermineNextJob_RoomContainment
    {
        private static readonly List<IntVec3> tmpRoomCells = new List<IntVec3>();

        public static bool Prefix(Pawn ___pawn, ref ThinkResult __result, out ThinkTreeDef thinkTree)
        {
            thinkTree = null;

            if (!Patch_RoomContainment.IsContainedInRoom(___pawn) || ___pawn.Downed)
            {
                return true;
            }

            Building_AbnormalityContainmentTerminal terminal = ___pawn.TryGetComp<CompAbnormalityHoldingPlatformTarget>()?.BoundTerminal;
            if (terminal == null || !terminal.Spawned || terminal.Map != ___pawn.Map)
            {
                return true;
            }

            bool canWander = ___pawn.TryGetComp<CompAbnormality>()?.Props.canWanderInContainmentRoom == true
                && ___pawn.TryGetComp<CompAbnormalityTool>() == null
                && ___pawn.GetStatValue(StatDefOf.MoveSpeed) > 0.01f;

            Job job = null;
            if (terminal.IsStudySessionActive)
            {
                job = JobMaker.MakeJob(RimWorld.JobDefOf.Wait, 60);
            }
            else if (canWander)
            {
                Room room = terminal.GetRoom();
                if (room != null && !room.TouchesMapEdge && !Rand.Chance(0.45f))
                {
                    tmpRoomCells.Clear();
                    foreach (IntVec3 c in room.Cells)
                    {
                        if (c != terminal.Position && c.InBounds(___pawn.Map) && c.Standable(___pawn.Map) && c.GetDoor(___pawn.Map) == null && ___pawn.CanReach(c, PathEndMode.OnCell, Danger.None))
                        {
                            tmpRoomCells.Add(c);
                        }
                    }

                    if (tmpRoomCells.Count > 0)
                    {
                        IntVec3 dest = tmpRoomCells.RandomElement();
                        tmpRoomCells.Clear();
                        job = JobMaker.MakeJob(RimWorld.JobDefOf.GotoWander, dest);
                        job.expiryInterval = Rand.Range(120, 240);
                        job.canBashDoors = false;
                    }
                }

                if (job == null)
                {
                    job = JobMaker.MakeJob(RimWorld.JobDefOf.Wait_Wander, Rand.Range(90, 180));
                }
            }
            else
            {
                IntVec3 standCell = terminal.GetContainmentSpawnCell();
                if (___pawn.Position != standCell
                    && standCell.IsValid
                    && ___pawn.GetStatValue(StatDefOf.MoveSpeed) > 0.01f
                    && ___pawn.CanReach(standCell, PathEndMode.OnCell, Danger.None))
                {
                    job = JobMaker.MakeJob(RimWorld.JobDefOf.Goto, standCell);
                    job.expiryInterval = 180;
                    job.canBashDoors = false;
                }
                else
                {
                    job = JobMaker.MakeJob(RimWorld.JobDefOf.Wait, 120);
                }
            }

            __result = new ThinkResult(job, null);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawGUIOverlay))]
    public static class Patch_Pawn_DrawGUIOverlay_EscapedAbnormality
    {
        public static void Postfix(Pawn __instance)
        {
            UI.EscapedAbnormalityOverlayDrawer.DrawEscapedOverlay(__instance);
        }
    }
}
