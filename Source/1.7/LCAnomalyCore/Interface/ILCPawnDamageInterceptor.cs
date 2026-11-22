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
}
