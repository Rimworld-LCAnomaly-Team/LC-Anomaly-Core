using Verse;

namespace LCAnomalyCore.Render
{
    /// <summary>表示 <c>PawnRenderNodeProperties_HediffUpHead</c> 类型。</summary>
    public class PawnRenderNodeProperties_HediffUpHead : PawnRenderNodeProperties
    {
        /// <summary>表示 <c>offsetRangeX</c>。</summary>
        public FloatRange offsetRangeX = FloatRange.Zero;

        /// <summary>表示 <c>offsetRangeZ</c>。</summary>
        public FloatRange offsetRangeZ = FloatRange.Zero;

        /// <summary>表示 <c>durationTicksRange</c>。</summary>
        public IntRange durationTicksRange = new IntRange(60, 60);

        /// <summary>表示 <c>nextSpasmTicksRange</c>。</summary>
        public IntRange nextSpasmTicksRange = new IntRange(60, 60);

        /// <summary>初始化 <c>PawnRenderNodeProperties_HediffUpHead</c> 类的新实例。</summary>
        public PawnRenderNodeProperties_HediffUpHead()
        {
            //PawnRenderNodeProperties_Spastic
            nodeClass = typeof(PawnRenderNode_HediffUpHead);
            workerClass = typeof(PawnRenderNodeWorker_HediffUpHead);
        }
    }
}