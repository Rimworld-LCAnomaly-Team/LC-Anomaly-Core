using HarmonyLib;
using LCAnomalyCore.Comp;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 当异常实体正处于房间收容状态且配置了 heldDrawScale（!= 1）时，
    /// 对整个 PawnRenderTree（含所有子节点偏移、动画位移与衍生头颅挂件等）应用等比缩放，
    /// 出逃或游荡于地图上时保持原始体型不变。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]
    public static class Patch_PawnRenderTree_HeldScale
    {
        /// <summary>在构建绘制矩阵前注入收容态缩放变换。</summary>
        public static void Prefix(PawnRenderTree __instance, ref PawnDrawParms parms)
        {
            if (parms.Portrait)
            {
                return;
            }

            Pawn pawn = parms.pawn ?? __instance?.pawn;
            CompAbnormalityHoldingPlatformTarget target = pawn?.TryGetComp<CompAbnormalityHoldingPlatformTarget>();
            if (target == null || !target.CurrentlyContainedInRoom)
            {
                return;
            }

            float scale = target.Props.heldDrawScale;
            if (scale > 0f && Mathf.Abs(scale - 1f) > 0.0001f)
            {
                parms.matrix *= Matrix4x4.Scale(new Vector3(scale, 1f, scale));
            }
        }
    }
}
