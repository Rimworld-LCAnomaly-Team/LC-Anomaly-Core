using LCAnomalyCore.Damage;

namespace LCAnomalyCore.Interface
{
    /// <summary>
    /// 供动态切换四色抗性的实体（如一无所有的三阶段形态变化）向核心伤害计算与面板展示提供实时倍率。
    /// </summary>
    public interface ILCFourColorDynamicResistance
    {
        /// <summary>尝试获取指定四色伤害类型的当前有效抗性倍率。</summary>
        bool TryGetDynamicFourColorResistance(FourColorDamageType damageType, out float resistance);
    }
}
