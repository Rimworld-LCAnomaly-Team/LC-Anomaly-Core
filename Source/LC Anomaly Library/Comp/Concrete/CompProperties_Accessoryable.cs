using Verse;

namespace LCAnomalyLibrary.Comp
{
    /// <summary>
    /// 可获取饰品的CompProperties
    /// </summary>
    public class CompProperties_Accessoryable : CompProperties
    {
        /// <summary>
        /// XML：饰品Def
        /// </summary>
        public ThingDef accessoryDef;

        /// <summary>
        /// XML：获取饰品的概率
        /// </summary>
        public float accessoryChance;

        /// <summary>
        /// XML: 饰品解锁所需观察等级
        /// </summary>
        public int unlockLevel = 2;

        /// <summary>
        /// Comp
        /// </summary>
        public CompProperties_Accessoryable()
        {
            compClass = typeof(CompAccessoryable);
        }
    }
}