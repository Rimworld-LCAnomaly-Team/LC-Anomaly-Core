using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using Verse;

namespace LCAnomalyCore.Interface
{
    /// <summary>
    /// 附加在 HediffComp 上的受击拦截接口，用于在 <c>Pawn.PreApplyDamage</c> 阶段拦截或改写伤害。
    /// </summary>
    public interface ILCPawnDamageInterceptor
    {
        /// <summary>尝试拦截即将施加给 <paramref name="pawn"/> 的伤害。</summary>
        /// <param name="pawn">原定受击的 Pawn。</param>
        /// <param name="dinfo">传入伤害信息（可改写）。</param>
        /// <param name="absorbed">若返回 true，需指明是否已完全吸收本次伤害。</param>
        /// <returns>返回 true 表示该拦截器已处理本次伤害事件。</returns>
        bool TryInterceptDamage(Pawn pawn, ref DamageInfo dinfo, out bool absorbed);
    }

    /// <summary>
    /// 附加在 HediffComp 上的受击后置处理接口，仅在所有 <see cref="ILCPawnDamageInterceptor"/> 均未吸收该次伤害后触发。
    /// </summary>
    public interface ILCPawnPostInterceptDamageHandler
    {
        /// <summary>在前置伤害拦截器（如异界肖像转嫁、子弹护盾）未吸收本次伤害时执行承伤记录或反噬判断。</summary>
        /// <param name="pawn">受击的 Pawn。</param>
        /// <param name="dinfo">经前置拦截器处理后的伤害信息。</param>
        /// <param name="absorbed">若返回 true，需指明是否已吸收本次伤害并中断原版伤害结算。</param>
        /// <returns>返回 true 表示该处理器已处理本次伤害事件。</returns>
        bool TryHandleDamageAfterIntercept(Pawn pawn, ref DamageInfo dinfo, out bool absorbed);
    }

    /// <summary>
    /// 附加在 HediffComp 上的异想体研究工作修正接口，用于在不修改持久化四维属性的前提下叠加研究成功率加成。
    /// </summary>
    public interface ILCPawnStudyModifier
    {
        /// <summary>返回该状态对指定工作类型异想体研究的成功率加成（例如 +0.10f 表示 +10%）。</summary>
        float GetStudySuccessRateBonus(Pawn studier, CompAbnormality abnormality, EAnomalyWorkType workType);
    }
}
