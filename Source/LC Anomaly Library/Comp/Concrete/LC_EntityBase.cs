using LCAnomalyLibrary.Util;
using RimWorld;
using Verse;

namespace LCAnomalyLibrary.Comp
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
        public LC_CompEntity EntityComp
        {
            get
            {
                if(entityComp == null)
                    return entityComp = GetComp<LC_CompEntity>();
                else
                    return entityComp;
            }
        }
        protected LC_CompEntity entityComp;


        public LC_EntityBasePawn()
        {
        } 

        public override void Notify_Studied(Pawn studier, float amount, KnowledgeCategoryDef category = null)
        {
            LogUtil.Warning("研究了一次");
            EntityComp?.Notify_Studied(studier);
        }

        /// <summary>
        /// 被收容时的Tick
        /// </summary>
        public virtual void TickHolded()
        {
        }
    }
}