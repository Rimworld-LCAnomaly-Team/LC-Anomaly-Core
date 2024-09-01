using Verse;

namespace LCAnomalyLibrary.Render.Normal
{
    public class PawnRenderNode_StillState : PawnRenderNode
    {
        public new PawnRenderNodeProperties_StillState Props => (PawnRenderNodeProperties_StillState)props;

        public PawnRenderNode_StillState(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            return GraphicDatabase.Get<Graphic_Single>(Props.texPath, ShaderDatabase.CutoutComplexBlend);
        }
    }
}
