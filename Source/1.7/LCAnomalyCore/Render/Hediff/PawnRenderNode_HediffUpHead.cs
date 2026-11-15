using UnityEngine;
using Verse;

namespace LCAnomalyCore.Render
{
    /// <summary>表示 <c>PawnRenderNode_HediffUpHead</c> 类型。</summary>
    public class PawnRenderNode_HediffUpHead : PawnRenderNode
    {
        /// <summary>表示 <c>SpasmData</c> 类型。</summary>
        public class SpasmData
        {
            /// <summary>表示 <c>offsetStart</c>。</summary>
            public Vector3 offsetStart;

            /// <summary>表示 <c>offsetTarget</c>。</summary>
            public Vector3 offsetTarget;

            /// <summary>表示 <c>tickStart</c>。</summary>
            public int tickStart;

            /// <summary>表示 <c>nextSpasm</c>。</summary>
            public int nextSpasm;

            /// <summary>表示 <c>duration</c>。</summary>
            public float duration;

            /// <summary>初始化 <c>SpasmData</c> 类的新实例。</summary>
            public SpasmData()
            {
                duration = 1f;
            }
        }

        /// <summary>表示 <c>spasmData</c>。</summary>
        protected SpasmData spasmData;

        /// <summary>初始化 <c>PawnRenderNode_HediffUpHead</c> 类的新实例。</summary>
        public PawnRenderNode_HediffUpHead(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <inheritdoc />
        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return new GraphicMeshSet(MeshPool.GridPlane(props.overrideMeshSize ?? props.drawSize));
        }

        /// <summary>执行 <c>CheckAndDoSpasm</c> 定义的操作。</summary>
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

        /// <summary>执行 <c>GetNextSpasmDurationTicks</c> 定义的操作。</summary>
        protected virtual int GetNextSpasmDurationTicks()
        {
            if (props is PawnRenderNodeProperties_HediffUpHead pawnRenderNodeProperties_HediffUpHead)
            {
                return pawnRenderNodeProperties_HediffUpHead.durationTicksRange.RandomInRange;
            }

            return 0;
        }

        /// <summary>执行 <c>GetNextSpasmTick</c> 定义的操作。</summary>
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