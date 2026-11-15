using LCAnomalyCore.Damage;
using Verse;

namespace LCAnomalyCore.ModExtensions
{
    /// <summary>表示 <c>FourColorDamageExtension</c> 类型。</summary>
    public class FourColorDamageExtension : DefModExtension
    {
        /// <summary>表示 <c>damageType</c>。</summary>
        public FourColorDamageType damageType;
        /// <summary>表示 <c>riskLevel</c>。</summary>
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
    }

    /// <summary>表示 <c>FourColorWeaponExtension</c> 类型。</summary>
    public class FourColorWeaponExtension : DefModExtension
    {
        /// <summary>表示 <c>damageType</c>。</summary>
        public FourColorDamageType damageType;
        /// <summary>表示 <c>riskLevel</c>。</summary>
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        /// <summary>表示 <c>damageMultiplier</c>。</summary>
        public float damageMultiplier = 1f;
    }

    /// <summary>表示 <c>FourColorApparelExtension</c> 类型。</summary>
    public class FourColorApparelExtension : DefModExtension
    {
        /// <summary>表示 <c>riskLevel</c>。</summary>
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        /// <summary>表示 <c>redResistance</c>。</summary>
        public float redResistance = 1f;
        /// <summary>表示 <c>whiteResistance</c>。</summary>
        public float whiteResistance = 1f;
        /// <summary>表示 <c>blackResistance</c>。</summary>
        public float blackResistance = 1f;
        /// <summary>表示 <c>paleResistance</c>。</summary>
        public float paleResistance = 1f;

        /// <summary>执行 <c>ResistanceFor</c> 定义的操作。</summary>
        public float ResistanceFor(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return redResistance;
                case FourColorDamageType.White: return whiteResistance;
                case FourColorDamageType.Black: return blackResistance;
                case FourColorDamageType.Pale: return paleResistance;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Optional race-level override for compatibility mods.
    /// </summary>
    public class FourColorTargetExtension : DefModExtension
    {
        /// <summary>表示 <c>targetKind</c>。</summary>
        public FourColorTargetKind targetKind = FourColorTargetKind.Default;
    }
}
