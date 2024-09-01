using HarmonyLib;
using RimWorld;
using Verse;

namespace LCAnomalyLibrary.Patch
{
    /// <summary>
    /// 关于AnomalyUtility的补丁（用于移除和添加图鉴）
    /// </summary>
    [HarmonyPatch(typeof(AnomalyUtility), nameof(AnomalyUtility.OpenCodexGizmo))]
    public class Patch_AnomalyUtility
    {
        /// <summary>
        /// Prefix方法
        /// </summary>
        /// <param name="thing">实体</param>
        /// <param name="__result">gizmo</param>
        /// <returns>是否中断原方法</returns>
        private static bool Prefix(Thing thing, ref Verse.Gizmo __result)
        {
            //是LC实体则添加LC Dialog，否则保持原版
            if (thing.def.entityCodexEntry is Defs.EntityCodexEntryDef)
            {
                //Log.Warning($"我是LC实体：{thing.GetType()}");
                __result = Util.Gizmos.Get_EntityCodex(thing);
                return false;
            }
            else
            {
                //Log.Warning($"我不是LC实体：{thing.GetType()}");
                return true;
            }
        }
    }
}