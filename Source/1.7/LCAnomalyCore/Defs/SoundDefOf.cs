using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 收容工作流程相关音效 DefOf
    /// </summary>
    [DefOf]
    public static class SoundDefOf
    {
        /// <summary>收容工作开始时播放的舱门锁定提示音</summary>
        public static SoundDef LC_Sound_ContainmentWork_Start;

        /// <summary>收容工作完成时播放的舱门解锁提示音</summary>
        public static SoundDef LC_Sound_ContainmentWork_Finish;

        /// <summary>未配置专属工作音效的异想体使用的默认工作循环音效</summary>
        public static SoundDef LC_Sound_ContainmentWork_DefaultLoop;

        /// <summary>工作中产出 PE-Box 时的轻提示音</summary>
        public static SoundDef LC_Sound_ContainmentWork_PeBox;

        /// <summary>工作中产出 NE-Box 时的警示音</summary>
        public static SoundDef LC_Sound_ContainmentWork_NeBox;

        static SoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundDefOf));
        }
    }
}
