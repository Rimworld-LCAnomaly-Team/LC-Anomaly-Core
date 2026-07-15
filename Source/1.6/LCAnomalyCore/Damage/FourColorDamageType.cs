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
}
