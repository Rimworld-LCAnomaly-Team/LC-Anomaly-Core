using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyLibrary.Render
{
    public class PawnRenderNodeWorker_StillState : PawnRenderNodeWorker
    {
        private static readonly Color LineColor = new Color(0.89f, 0.21f, 0.13f);

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 vector = base.OffsetFor(node, parms, out pivot);
            //if (!(node.Props is PawnRenderNodeProperties_StillState pawnRenderNodeProperties_StillState))
            //{
            //    return vector;
            //}

            //vector += pawnRenderNodeProperties_StillState.offset.ToVector3() / 6f;
            //return new Vector3(vector.x, 0f, vector.z);

            return vector;
        }

        public override MaterialPropertyBlock GetMaterialPropertyBlock(PawnRenderNode node, Material material, PawnDrawParms parms)
        {
            MaterialPropertyBlock materialPropertyBlock = base.GetMaterialPropertyBlock(node, material, parms);
            if (!parms.pawn.TryGetComp<CompActivity>(out var comp))
            {
                return materialPropertyBlock;
            }

            Color lineColor = LineColor;
            lineColor.a = Mathf.Clamp01(comp.ActivityLevel);
            materialPropertyBlock.SetColor(ShaderPropertyIDs.ColorTwo, lineColor);
            return materialPropertyBlock;
        }
    }
}
