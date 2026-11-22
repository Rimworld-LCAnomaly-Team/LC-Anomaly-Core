using RimWorld;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC异想体蛋基础Comp
    /// </summary>
    public class LC_CompEgg : CompInteractable
    {
        public int biosignature;

        public override void PostPostMake()
        {
            base.PostPostMake();
            biosignature = Rand.Int;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref biosignature, "biosignature");
        }
        /// <summary>获取 <c>Props</c>。</summary>
        public new LC_CompProperties_InteractableEgg Props => (LC_CompProperties_InteractableEgg)props;

        /// <summary>
        /// 交互时触发
        /// </summary>
        /// <param name="caster"></param>
        protected override void OnInteracted(Pawn caster)
        {
            CompUsable comp = parent.GetComp<CompUsable>();
            comp.TryStartUseJob(caster, comp.GetExtraTarget(caster));
        }

        /// <summary>
        /// 浮动菜单选项
        /// </summary>
        /// <param name="selPawn"></param>
        /// <returns></returns>
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            yield break;
        }

        /// <inheritdoc />
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent != null)
            {
                yield return Util.Gizmos.Get_EntityCodex(parent);
            }
        }
    }
}
