using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC可提取EGO CompProperties
    /// </summary>
    public class CompProperties_EgoExtractable : CompProperties
    {
        #region XML字段

        /// <summary>
        /// XML：EGO武器
        /// </summary>
        public ThingDef weaponExtracted;

        /// <summary>
        /// XML：EGO装备
        /// </summary>
        public ThingDef armorExtracted;

        /// <summary>
        /// XML: EGO武器提取图标路径
        /// </summary>
        public string weaponIconPath;

        /// <summary>
        /// XML: EGO装备提取图标路径
        /// </summary>
        public string armorIconPath;

        /// <summary>
        /// XML：可提取EGO武器的上限数量
        /// </summary>
        public int amountMaxWeapon;

        /// <summary>
        /// XML：可提取EGO装备的上限数量
        /// </summary>
        public int amountMaxArmor;

        /// <summary>
        /// XML: 提取EGO武器所需PeBox数量
        /// </summary>
        public int weaponExtractedNeed;

        /// <summary>
        /// XML: 提取EGO装备所需PeBox数量
        /// </summary>
        public int armorExtractedNeed;

        /// <summary>
        /// XML: EGO解锁所需观察等级
        /// </summary>
        public int unlockLevel = 2;

        #endregion XML字段

        /// <summary>
        /// Comp
        /// </summary>
        public CompProperties_EgoExtractable()
        {
            compClass = typeof(CompEgoExtractable);
        }
    }
}