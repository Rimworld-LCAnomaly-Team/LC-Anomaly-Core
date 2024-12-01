using UnityEngine;
using Verse;

namespace LCAnomalyCore.Render
{
    public class PawnRenderNodeWorker_HediffUpHead : PawnRenderNodeWorker
    {
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 result = base.OffsetFor(node, parms, out pivot);
            if (node is PawnRenderNode_HediffUpHead pawnRenderNode_HediffUpHead && pawnRenderNode_HediffUpHead.CheckAndDoSpasm(parms, out var dat, out var progress))
            {
                result += Vector3.Lerp(dat.offsetStart, dat.offsetTarget, progress);
                //Log.Warning("!");
            }

            return result;
        }
    }
}