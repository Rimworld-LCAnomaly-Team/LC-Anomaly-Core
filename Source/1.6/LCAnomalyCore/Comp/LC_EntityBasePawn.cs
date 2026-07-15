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
        /// <summary>初始化 <c>LC_EntityBaseThing</c> 类的新实例。</summary>
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

        /// <summary>表示 <c>entityComp</c>。</summary>
        protected CompAbnormality entityComp;

        /// <summary>初始化 <c>LC_EntityBasePawn</c> 类的新实例。</summary>
        public LC_EntityBasePawn()
        {
        }

        /// <inheritdoc />
        public override void Notify_Studied(Pawn studier, float amount, KnowledgeCategoryDef category = null)
        {
            LogUtil.Warning("研究了一次");

            if (EntityComp != null)
            {
                EntityComp.Notify_Studied(studier);
                EntityComp.StudyUnlocksComp?.OnStudied(studier, amount, category);
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
