using LCAnomalyCore.Util;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC实体Thing基类
    /// </summary>
    public class LC_EntityBaseThing : ThingWithComps
    {
        public LC_EntityBaseThing()
        {
        }
    }

    /// <summary>
    /// LC实体Pawn基类
    /// </summary>
    public class LC_EntityBasePawn : Pawn
    {
        /// <summary>
        /// LC的实体Comp
        /// </summary>
        public CompAbnormality EntityComp
        {
            get
            {
                if (entityComp == null)
                    return entityComp = GetComp<CompAbnormality>();
                else
                    return entityComp;
            }
        }

        protected CompAbnormality entityComp;

        public LC_EntityBasePawn()
        {
        }

        public override void Notify_Studied(Pawn studier, float amount, KnowledgeCategoryDef category = null)
        {
            LogUtil.Warning("研究了一次");

            if (EntityComp != null)
            {
                EntityComp.Notify_Studied(studier);
                EntityComp.StudyUnlocksComp.OnStudied(studier, amount, category);
            }
        }

        /// <summary>
        /// 被收容时的Tick
        /// </summary>
        public virtual void TickHolded()
        {
        }
    }
}