using LCAnomalyLibrary.Render.Normal;
using UnityEngine;
using Verse;

namespace LCAnomalyLibrary.Render
{
    public class PawnRenderNodeProperties_StillState : PawnRenderNodeProperties
    {
        public Vector2 offset;

        public PawnRenderNodeProperties_StillState()
        {
            nodeClass = typeof(PawnRenderNode_StillState);
            workerClass = typeof(PawnRenderNodeWorker_StillState);
            drawSize = new Vector2(1.35f, 1.35f);
        }
    }
}
