using HarmonyLib;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 服装层Patch（用于解决头部服装渲染问题）
    /// </summary>
    [HarmonyPatch(typeof(ApparelProperties), nameof(ApparelProperties.LastLayer), MethodType.Getter)]
    public class Patch_ApparelProperties
    {
        private static bool Prefix(ApparelProperties __instance, ref ApparelLayerDef __result)
        {
            //如果是自定义的头部层就截断原方法，强制返回为原版的头部层
            var layers = __instance.layers;
            if (layers.Count > 0)
            {
                var tmpLayer = layers[layers.Count - 1];
                if (tmpLayer == Defs.ApparelLayerDefOf.LC_AccessoryMouth || tmpLayer == Defs.ApparelLayerDefOf.LC_AccessoryHead)
                {
                    __result = ApparelLayerDefOf.Overhead;
                    return false;
                }
            }

            //如果不是就执行原方法
            return true;
        }
    }
}