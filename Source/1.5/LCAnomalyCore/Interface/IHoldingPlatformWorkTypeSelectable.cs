using LCAnomalyCore.Util;

namespace LCAnomalyCore.Interface
{
    public interface IHoldingPlatformWorkTypeSelectable
    {
        public EAnomalyWorkType CurWorkType { get; set; }
    }
}