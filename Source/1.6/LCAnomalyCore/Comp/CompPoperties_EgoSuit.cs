using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompPoperties_EgoSuit</c> 类型。</summary>
    public class CompPoperties_EgoSuit : CompProperties
    {
        /// <summary>表示 <c>level</c>。</summary>
        public string level = "ZAYIN";

        /// <summary>表示 <c>redResist</c>。</summary>
        public float redResist = 1.0f;
        /// <summary>表示 <c>whiteResist</c>。</summary>
        public float whiteResist = 1.0f;
        /// <summary>表示 <c>blackResist</c>。</summary>
        public float blackResist = 1.0f;
        /// <summary>表示 <c>paleResist</c>。</summary>
        public float paleResist = 1.0f;

        /// <summary>初始化 <c>CompPoperties_EgoSuit</c> 类的新实例。</summary>
        public CompPoperties_EgoSuit()
        {
            compClass = typeof(CompEgoSuit);
        }
    }

    /// <summary>
    /// Correctly-spelled XML alias. The old class name remains for existing Defs.
    /// </summary>
    public class CompProperties_EgoSuit : CompPoperties_EgoSuit
    {
    }
}
