using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Building
{
    public class Building_CogitoBucket : Verse.Building
    {
        public CompCogitoBucketSpawner CompSpawner => this.GetComp<CompCogitoBucketSpawner>();

        private float animOffset = 0f;

        private bool shouldUp = true;

        /// <summary>
        /// 绘制方法
        /// </summary>
        /// <param name="drawLoc">位置</param>
        /// <param name="flip">是否翻转</param>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            if (CompSpawner != null)
            {
                if (CompSpawner.HasRequireThingInstalled)
                {
                    if (animOffset >= 0.05f)
                        shouldUp = false;
                    if (animOffset <= -0.05f)
                        shouldUp = true;
                    if (shouldUp)
                        animOffset += 0.0001f;
                    else
                        animOffset -= 0.0001f;

                    GraphicUtil.CogitoBucket_GetCachedGraphic("BrainSpinalNerve")
                        .Draw(new Vector3(this.DrawPos.x, this.DrawPos.y, this.DrawPos.z + animOffset) + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
                }
            }
            else
            {
                Log.Error("CompCogitoBucketSpawner is null");
            }

            GraphicUtil.CogitoBucket_GetCachedGraphic("Glass")
                .Draw(this.DrawPos + Altitudes.AltIncVect * 2f, base.Rotation, this, 0f);
        }
    }
}