using RimWorld;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该Mod所有的ExtractRitualRoleDef
    /// </summary>
    [DefOf]
    public static class ExtractRitualRoleDefOf
    {
        static ExtractRitualRoleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ExtractRitualRoleDefOf));
        }

        /// <summary>
        /// ZAYIN异常提取仪式
        /// </summary>
        public static PsychicRitualRoleDef ExtractLCAnomalyZAYIN;

        /// <summary>
        /// TETH异常提取仪式
        /// </summary>
        public static PsychicRitualRoleDef ExtractLCAnomalyTETH;

        /// <summary>
        /// HE异常提取仪式
        /// </summary>
        public static PsychicRitualRoleDef ExtractLCAnomalyHE;

        /// <summary>
        /// WAW异常提取仪式
        /// </summary>
        public static PsychicRitualRoleDef ExtractLCAnomalyWAW;

        /// <summary>
        /// ALEPH异常提取仪式
        /// </summary>
        public static PsychicRitualRoleDef ExtractLCAnomalyALEPH;
    }
}
