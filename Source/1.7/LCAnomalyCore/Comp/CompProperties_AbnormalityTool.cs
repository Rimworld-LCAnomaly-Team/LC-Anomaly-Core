using LCAnomalyCore.Comp.Pawns;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>工具型异想体使用模式。</summary>
    public enum AbnormalityToolMode
    {
        /// <summary>单次交互触发型（如异界肖像、行为矫正仪）。</summary>
        SingleUse,

        /// <summary>持续驻留使用型（如血肉偶像、我们可以改变一切）。</summary>
        Continuous,

        /// <summary>可佩戴/携带饰品工具型。</summary>
        Equippable
    }

    /// <summary>工具型异想体研究笔记解锁度量标准。</summary>
    public enum ToolStudyMetric
    {
        /// <summary>按累计使用次数解锁。</summary>
        UseCount,

        /// <summary>按累计使用时长（秒）解锁。</summary>
        UseSeconds
    }

    /// <summary>工具型异想体通用组件配置。</summary>
    public class CompProperties_AbnormalityTool : CompProperties
    {
        public AbnormalityToolMode toolMode = AbnormalityToolMode.SingleUse;
        public ToolStudyMetric studyMetric = ToolStudyMetric.UseCount;

        public int useDurationTicks = 120;
        public int cooldownTicks = 0;

        public int continuousTickInterval = 60;
        public int maxContinuousDurationTicks = 0;
        public bool allowManualStopContinuous = true;
        /// <summary>持续使用时是否将殖民者实际收入内部封闭容器并从地图隐藏。</summary>
        public bool enclosesUserInside = false;
        public string stopGizmoLabelKey = "LC_Tool_StopGizmoLabel";
        public string stopGizmoDescKey = "LC_Tool_StopGizmoDesc";
        public float expGainPerContinuousSecond = 0f;

        public string useLabelKey = "LC_Tool_UseFloatMenu";
        public string useGizmoLabelKey = "LC_Tool_UseGizmoLabel";
        public string useGizmoDescKey = "LC_Tool_UseGizmoDesc";

        [NoTranslate]
        public string useIconPath;

        /// <summary>离开收容平台后还原成的异想体蛋定义。</summary>
        public ThingDef eggDef;

        public SoundDef soundUsing;
        public SoundDef soundUsed;

        public EPawnStatus expStatusType = EPawnStatus.Prudence;
        public float expGainOnUse = 0f;

        public int minFortitudeLevel = 0;
        public int minPrudenceLevel = 0;
        public int minTemperanceLevel = 0;
        public int minJusticeLevel = 0;

        public CompProperties_AbnormalityTool()
        {
            compClass = typeof(CompAbnormalityTool);
        }
    }
}
