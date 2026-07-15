using LCAnomalyCore.GameComponent;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>表示 <c>Components</c> 类型。</summary>
    public static class Components
    {
        /// <summary>获取 <c>LC</c>。</summary>
        public static GameComponent_LC LC => Current.Game?.GetComponent<GameComponent_LC>();
    }
}
