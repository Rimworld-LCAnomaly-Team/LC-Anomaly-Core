using UnityEngine;
using Verse;

namespace LCAnomalyCore.Render
{
    /// <summary>表示 <c>PawnRenderNodeProperties_StillState</c> 类型。</summary>
    public class PawnRenderNodeProperties_StillState : PawnRenderNodeProperties
    {
        /// <summary>表示 <c>offset</c>。</summary>
        public Vector2 offset;

        /// <summary>初始化 <c>PawnRenderNodeProperties_StillState</c> 类的新实例。</summary>
        public PawnRenderNodeProperties_StillState()
        {
            nodeClass = typeof(PawnRenderNode_StillState);
            workerClass = typeof(PawnRenderNodeWorker_StillState);
            drawSize = new Vector2(1.35f, 1.35f);
        }
    }
}