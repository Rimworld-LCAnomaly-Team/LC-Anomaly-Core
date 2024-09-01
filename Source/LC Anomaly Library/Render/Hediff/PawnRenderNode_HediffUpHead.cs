using UnityEngine;
using Verse;

namespace LCAnomalyLibrary.Render
{
    public class PawnRenderNode_HediffUpHead : PawnRenderNode
    {
        public class SpasmData
        {
            public Vector3 offsetStart;

            public Vector3 offsetTarget;

            public int tickStart;

            public int nextSpasm;

            public float duration;

            public SpasmData()
            {
                duration = 1f;
            }
        }

        protected SpasmData spasmData;

        public PawnRenderNode_HediffUpHead(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return new GraphicMeshSet(MeshPool.GridPlane(props.overrideMeshSize ?? props.drawSize));
        }

        public bool CheckAndDoSpasm(PawnDrawParms parms, out SpasmData dat, out float progress)
        {
            if (parms.pawn.Dead || !(props is PawnRenderNodeProperties_HediffUpHead pawnRenderNodeProperties_HediffUpHead) || parms.Portrait || parms.Cache)
            {
                progress = 0f;
                dat = null;
                return false;
            }

            if (spasmData == null)
            {
                spasmData = new SpasmData();
            }

            if (Find.TickManager.TicksGame >= spasmData.nextSpasm)
            {
                spasmData.tickStart = Find.TickManager.TicksGame;
                spasmData.duration = GetNextSpasmDurationTicks();
                spasmData.nextSpasm = GetNextSpasmTick();
                spasmData.offsetStart = spasmData.offsetTarget;
                spasmData.offsetTarget = new Vector3(pawnRenderNodeProperties_HediffUpHead.offsetRangeX.RandomInRange, 0f, pawnRenderNodeProperties_HediffUpHead.offsetRangeZ.RandomInRange);
            }

            progress = (float)(Find.TickManager.TicksGame - spasmData.tickStart) / Mathf.Max(spasmData.duration, 0.0001f);
            dat = spasmData;
            return true;
        }

        protected virtual int GetNextSpasmDurationTicks()
        {
            if (props is PawnRenderNodeProperties_HediffUpHead pawnRenderNodeProperties_HediffUpHead)
            {
                return pawnRenderNodeProperties_HediffUpHead.durationTicksRange.RandomInRange;
            }

            return 0;
        }

        protected virtual int GetNextSpasmTick()
        {
            if (props is PawnRenderNodeProperties_HediffUpHead pawnRenderNodeProperties_HediffUpHead)
            {
                return spasmData.tickStart + (int)spasmData.duration + pawnRenderNodeProperties_HediffUpHead.nextSpasmTicksRange.RandomInRange;
            }

            return 0;
        }
    }
}
