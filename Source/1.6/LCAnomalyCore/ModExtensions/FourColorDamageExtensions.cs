using LCAnomalyCore.Damage;
using Verse;

namespace LCAnomalyCore.ModExtensions
{
    public class FourColorDamageExtension : DefModExtension
    {
        public FourColorDamageType damageType;
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
    }

    public class FourColorWeaponExtension : DefModExtension
    {
        public FourColorDamageType damageType;
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        public float damageMultiplier = 1f;
    }

    public class FourColorApparelExtension : DefModExtension
    {
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        public float redResistance = 1f;
        public float whiteResistance = 1f;
        public float blackResistance = 1f;
        public float paleResistance = 1f;

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
        public FourColorTargetKind targetKind = FourColorTargetKind.Default;
    }
}
