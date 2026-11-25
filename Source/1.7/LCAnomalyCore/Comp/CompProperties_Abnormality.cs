using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>
    /// LC基础实体CompProperties
    /// </summary>
    public class CompProperties_Abnormality : CompProperties
    {
        #region XML字段

        /// <summary>
        /// XML：最大逆卡巴拉值
        /// </summary>
        public int qliphothCountMax;

        /// <summary>
        /// XML：学习基础成功率
        /// </summary>
        public float studySucessRateBase;

        /// <summary>
        /// XML：出逃时是否通知
        /// </summary>
        public bool shouldNotifyWhenEscape;

        /// <summary>
        /// XML：提供出逃点数（决定是否提供点数，不提供的情况下自定义点数无效）
        /// </summary>
        public bool ifProvideWarningPoints;

        /// <summary>
        /// XML：自定义出逃警报点数（填大于0则启用自定义点数，否则按异常等级计算）
        /// </summary>
        public int customWarningPoints;

        /// <summary>
        /// XML：被研究时播放的音效
        /// </summary>
        public SoundDef soundWorking;

        /// <summary>
        /// XML：在房间内收容时是否允许自由踱步（生物型为 true，静物/工具型为 false）
        /// </summary>
        public bool canWanderInContainmentRoom = false;

        #endregion XML字段

        /// <summary>
        /// Comp
        /// </summary>
        public CompProperties_Abnormality()
        {
            compClass = typeof(CompAbnormality);
        }
    }
}