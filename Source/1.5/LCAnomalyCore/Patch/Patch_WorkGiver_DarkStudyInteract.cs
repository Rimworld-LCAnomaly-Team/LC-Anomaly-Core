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
        private static bool Prefix(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.def is Defs.LC_HoldingPlatformDef)
            {
                return false;
            }

            return true;
        }
    }
}