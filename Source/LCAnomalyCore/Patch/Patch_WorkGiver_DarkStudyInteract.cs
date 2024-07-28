using HarmonyLib;
using RimWorld;
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
            //非强制情况下，如果是LC平台则取消研究工作
            if (!forced && t.def is Defs.LC_HoldingPlatformDef)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
