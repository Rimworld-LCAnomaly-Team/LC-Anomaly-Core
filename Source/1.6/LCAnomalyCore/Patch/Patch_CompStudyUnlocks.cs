using HarmonyLib;
using LCAnomalyCore.Comp;
using LCAnomalyCore.GameComponent;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 关于ConpStudyUnlocks的补丁（为了插入改名方法）
    /// </summary>
    [HarmonyPatch(typeof(CompStudyUnlocks), "RegisterStudyLevel")]
    public class Patch_CompStudyUnlocks
    {
        /// <summary>
        /// Postfix
        /// </summary>
        private static void Postfix(CompStudyUnlocks __instance, Pawn studier, int i)
        {
            //Log.Warning("CompStudyUnlocks.RegisterStudyLevel 注入成功");

            //更新研究进度

            var component = Util.Components.LC;
            component.TryGetAnomalyStatusSaved(__instance.parent.def, out AnomalyStatusSaved saved);
            saved.StudyProgress = i + 1;
            component.AnomalyStatusSavedDict[__instance.parent.def] = saved;

            //进行名称检查
            var lcComp = __instance as CompAbnormalityStudyUnlocks;
            lcComp?.UnlockNameCheck();
        }
    }
}