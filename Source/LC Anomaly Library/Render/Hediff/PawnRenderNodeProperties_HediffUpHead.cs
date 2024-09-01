using Verse;

namespace LCAnomalyLibrary.Render
{
    public class PawnRenderNodeProperties_HediffUpHead : PawnRenderNodeProperties
    {
        public FloatRange offsetRangeX = FloatRange.Zero;

        public FloatRange offsetRangeZ = FloatRange.Zero;

        public IntRange durationTicksRange = new IntRange(60, 60);

        public IntRange nextSpasmTicksRange = new IntRange(60, 60);

        public PawnRenderNodeProperties_HediffUpHead()
        {
            //PawnRenderNodeProperties_Spastic
            nodeClass = typeof(PawnRenderNode_HediffUpHead);
            workerClass = typeof(PawnRenderNodeWorker_HediffUpHead);
        }
    }
}
