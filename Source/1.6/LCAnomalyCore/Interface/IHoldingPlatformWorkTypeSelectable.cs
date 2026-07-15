using LCAnomalyCore.Util;

namespace LCAnomalyCore.Interface
{
    /// <summary>表示 <c>IHoldingPlatformWorkTypeSelectable</c> 类型。</summary>
    public interface IHoldingPlatformWorkTypeSelectable
    {
        /// <summary>获取或设置 <c>CurWorkType</c>。</summary>
        public EAnomalyWorkType CurWorkType { get; set; }
    }
}