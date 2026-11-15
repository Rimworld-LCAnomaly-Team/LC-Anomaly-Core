using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompEgoWeapon</c> 类型。</summary>
    public class CompEgoWeapon : ThingComp
    {
        /// <summary>获取 <c>Props</c>。</summary>
        public CompProperties_EgoWeapon Props => (CompProperties_EgoWeapon)props;
    }
}