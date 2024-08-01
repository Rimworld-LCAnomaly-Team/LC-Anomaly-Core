using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于WorkGiver_DarkStudyInteract的补丁（用于禁止主动研究LC实体）
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_DarkStudyInteract), nameof(WorkGiver_DarkStudyInteract.HasJobOnThing))]
    public class Patch_WorkGiver_DarkStudyInteract
    {
        private static bool Prefix(ref bool __result, Pawn pawn, Thing t, bool forced = false)
        {
            if (t.def is Defs.LC_HoldingPlatformDef)
            {
                return false;
            }

            ////如果是LC平台
            //if (t.def is Defs.LC_HoldingPlatformDef)
            //{
            //    //如果是LC收容平台建筑，且非右键强制状态
            //    if (!forced && t is Building.Building_HoldingPlatform building)
            //    {
            //        //如果分配列表里有该小人，那就允许自动研究，返回原方法
            //        if (building.CompAssignable.AssignedPawns.Contains(pawn))
            //        {
            //            return true;
            //        }
            //        //列表里没有该小人，那就不允许自动研究
            //        else
            //        {
            //            __result = false;
            //            return false;
            //        }
            //    }
            //}

            return true;
        }
    }
}