using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// Cogito被注射状态的HediffCompProperties
    /// </summary>
    public class HediffCompProperties_CogitoInjected : HediffCompProperties
    {
        /// <summary>
        /// XML：注射后直接死亡概率（单独判定）
        /// </summary>
        public float deadChance = 1f;

        /// <summary>
        /// XML：注射后大罪生物生成概率（单独判定）
        /// </summary>
        public float sevenSinEnitityChance = 1f;

        /// <summary>
        /// XML：注射后ZAYIN异常生成概率（单独判定）
        /// </summary>
        public float zayinChance = 1f;

        /// <summary>
        /// XML：注射后TETH异常生成概率（单独判定）
        /// </summary>
        public float tethChance = 1f;

        /// <summary>
        /// Comp
        /// </summary>
        public HediffCompProperties_CogitoInjected()
        {
            compClass = typeof(HediffComp_CogitoInjected);
        }
    }
}