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

        /// <summary>逆卡巴拉熔毁波次触发警报音</summary>
        public static SoundDef LC_Sound_QliphothMeltdown_Alert;

        /// <summary>逆卡巴拉熔毁被成功压制解除的提示音</summary>
        public static SoundDef LC_Sound_QliphothMeltdown_Clear;

        /// <summary>逆卡巴拉熔毁倒计时耗尽导致突破的失败警报音</summary>
        public static SoundDef LC_Sound_QliphothMeltdown_Fail;

        /// <summary>异想体提取界面启动音</summary>
        public static SoundDef LC_Sound_Extraction_Start;

        /// <summary>异想体提取三箱垂降就位音</summary>
        public static SoundDef LC_Sound_Extraction_DropIn;

        /// <summary>异想体提取鼠标悬停箱体音</summary>
        public static SoundDef LC_Sound_Extraction_Hover;

        /// <summary>异想体提取选中神秘匣音</summary>
        public static SoundDef LC_Sound_Extraction_Select;

        /// <summary>异想体提取确认完成音</summary>
        public static SoundDef LC_Sound_Extraction_Confirm;

        /// <summary>异想体提取重新抽取轮盘音</summary>
        public static SoundDef LC_Sound_Extraction_Reroll;

        /// <summary>异想体收容匣具现化落地声</summary>
        public static SoundDef LC_Sound_Extraction_Manifest;

        static SoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundDefOf));
        }
    }
}
