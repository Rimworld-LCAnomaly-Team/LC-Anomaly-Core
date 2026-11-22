namespace LCAnomalyCore.Damage
{
    /// <summary>表示 <c>FourColorDamageType</c> 类型。</summary>
    public enum FourColorDamageType
    {
        /// <summary>表示 <c>None</c>。</summary>
        None,
        /// <summary>表示 <c>Red</c>。</summary>
        Red,
        /// <summary>表示 <c>White</c>。</summary>
        White,
        /// <summary>表示 <c>Black</c>。</summary>
        Black,
        /// <summary>表示 <c>Pale</c>。</summary>
        Pale
    }

    /// <summary>表示 <c>LCRiskLevel</c> 类型。</summary>
    public enum LCRiskLevel
    {
        /// <summary>表示 <c>ZAYIN</c>。</summary>
        ZAYIN = 1,
        /// <summary>表示 <c>TETH</c>。</summary>
        TETH,
        /// <summary>表示 <c>HE</c>。</summary>
        HE,
        /// <summary>表示 <c>WAW</c>。</summary>
        WAW,
        /// <summary>表示 <c>ALEPH</c>。</summary>
        ALEPH
    }

    /// <summary>表示 <c>FourColorTargetKind</c> 类型。</summary>
    public enum FourColorTargetKind
    {
        /// <summary>表示 <c>Default</c>。</summary>
        Default,
        /// <summary>表示 <c>Employee</c>。</summary>
        Employee,
        /// <summary>表示 <c>Abnormality</c>。</summary>
        Abnormality
    }

    /// <summary>四色伤害实际命中目标时显示的抗性等级分类。</summary>
    public enum FourColorResistanceTier
    {
        /// <summary>普通 (1.0x)</summary>
        Normal,
        /// <summary>免疫 (0.0x)</summary>
        Immune,
        /// <summary>抗性 (0.01x ~ 0.49x)</summary>
        Resist,
        /// <summary>耐受 (0.50x ~ 0.99x)</summary>
        Endure,
        /// <summary>弱点 (1.01x ~ 1.99x)</summary>
        Weak,
        /// <summary>致命 (>= 2.00x)</summary>
        Fatal
    }
}
