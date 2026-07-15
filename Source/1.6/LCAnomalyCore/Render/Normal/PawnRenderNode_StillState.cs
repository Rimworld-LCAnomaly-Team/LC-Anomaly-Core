using Verse;

namespace LCAnomalyCore.Render
{
    /// <summary>表示 <c>PawnRenderNode_StillState</c> 类型。</summary>
    public class PawnRenderNode_StillState : PawnRenderNode
    {
        /// <summary>获取 <c>Props</c>。</summary>
        public new PawnRenderNodeProperties_StillState Props => (PawnRenderNodeProperties_StillState)props;

        /// <summary>初始化 <c>PawnRenderNode_StillState</c> 类的新实例。</summary>
        public PawnRenderNode_StillState(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        /// <inheritdoc />
        public override Graphic GraphicFor(Pawn pawn)
        {
            return GraphicDatabase.Get<Graphic_Single>(Props.texPath, ShaderDatabase.CutoutComplexBlend);
        }
    }
}