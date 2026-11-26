using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该mod所有的ThingDef
    /// </summary>
    [DefOf]
    public static class ThingDefOf
    {
        /// <summary>
        /// 通用PeBox
        /// </summary>
        public static ThingDef EnkephalinBox;

        /// <summary>
        /// 脑啡肽（成瘾品）
        /// </summary>
        public static ThingDef Enkephalin;

        /// <summary>
        /// Cogito
        /// </summary>
        public static ThingDef Cogito;

        /// <summary>
        /// 脑与脊髓神经
        /// </summary>
        public static ThingDef BrainSpinalNerve;

        /// <summary>
        /// LC房间收容控制终端
        /// </summary>
        public static ThingDef LC_ContainmentTerminal;

        /// <summary>
        /// LC收容室专用门
        /// </summary>
        public static ThingDef LC_ContainmentDoor;

        /// <summary>
        /// Cogito水桶（建筑）
        /// </summary>
        public static ThingDef CogitoBucket;

        /// <summary>
        /// 异想体提取井（建筑）
        /// </summary>
        public static ThingDef LC_ExtractionWell;

        /// <summary>
        /// 控制部核心（建筑）
        /// </summary>
        public static ThingDef LC_DepartmentCore_ControlTeam;

        /// <summary>
        /// 四色伤害组合飘字 Mote
        /// </summary>
        public static ThingDef LC_Mote_FourColorDamage;

        /// <summary>
        /// 收容工作期间浮现旁白飘字 Mote
        /// </summary>
        public static ThingDef LC_Mote_WorkNarration;

        static ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
        }
    }
}