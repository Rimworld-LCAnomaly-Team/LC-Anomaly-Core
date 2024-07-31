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
            //非强制情况下，如果是LC平台则判断自动研究列表条件
            if (!forced && t.def is Defs.LC_HoldingPlatformDef)
            {
                //如果分配列表里有该小人，那就允许自动研究，返回原方法
                if (t is Building.Building_HoldingPlatform building && building.CompAssignable.AssignedPawns.Contains(pawn))
                    return true;

                //列表里没有该小人，那就不允许自动研究
                __result = false;
                return false;
            }

            return true;
        }
    }
}