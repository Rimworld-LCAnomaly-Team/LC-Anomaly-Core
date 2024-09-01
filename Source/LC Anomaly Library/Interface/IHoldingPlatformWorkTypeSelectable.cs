using LCAnomalyLibrary.Util;

namespace LCAnomalyLibrary.Interface
{
    public interface IHoldingPlatformWorkTypeSelectable
    {
        public EAnomalyWorkType CurWorkType { get; set; }
    }
}