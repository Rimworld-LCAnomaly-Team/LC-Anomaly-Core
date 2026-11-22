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
            //如果是自定义的异想体饰品层就截断原方法，强制返回为原版Overhead层以共用无体型后缀贴图
            var layers = __instance?.layers;
            if (layers != null && layers.Count > 0)
            {
                var tmpLayer = layers[layers.Count - 1];
                if (IsCustomAccessoryLayer(tmpLayer))
                {
                    __result = ApparelLayerDefOf.Overhead;
                    return false;
                }
            }

            //如果不是就执行原方法
            return true;
        }

        private static bool IsCustomAccessoryLayer(ApparelLayerDef layer)
        {
            if (layer == null) return false;
            return layer == Defs.ApparelLayerDefOf.LC_AccessoryBackHead
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryNeck
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryMouth
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryMask
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryCheek
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryFace
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryHand
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryGlove
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryHead
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryHat
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryChest
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryEye
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryBack_Right
                || layer == Defs.ApparelLayerDefOf.LC_AccessoryBack_Left;
        }
    }
}