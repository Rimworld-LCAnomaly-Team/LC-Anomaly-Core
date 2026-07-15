using LCAnomalyCore.Damage;
using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>CompEgoSuit</c> 类型。</summary>
    public class CompEgoSuit : ThingComp
    {
        /// <summary>获取 <c>Props</c>。</summary>
        public CompPoperties_EgoSuit Props => (CompPoperties_EgoSuit)props;

        /// <summary>执行 <c>ResistanceFor</c> 定义的操作。</summary>
        public float ResistanceFor(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return Props.redResist;
                case FourColorDamageType.White: return Props.whiteResist;
                case FourColorDamageType.Black: return Props.blackResist;
                case FourColorDamageType.Pale: return Props.paleResist;
                default: return 1f;
            }
        }

        /// <summary>
        /// Retained for binary compatibility with early integrations. New damage workers apply
        /// resistance centrally and do not call this method.
        /// </summary>
        public void ApplyEGOResist(ref DamageInfo dinfo)
        {
            CompProperties_EgoWeapon weaponProps = dinfo.Weapon?.comps?
                .Find(x => x is CompProperties_EgoWeapon) as CompProperties_EgoWeapon;
            int attackLevel = weaponProps != null ? Util.DamageUtils.LevelTag2Int(weaponProps.level) : 1;
            int defenseLevel = Util.DamageUtils.LevelTag2Int(Props.level);
            FourColorDamageType damageType = FourColorDamageType.None;
            if (dinfo.Def == Defs.DamageDefOf.LC_RedDamage) damageType = FourColorDamageType.Red;
            else if (dinfo.Def == Defs.DamageDefOf.LC_WhiteDamage) damageType = FourColorDamageType.White;
            else if (dinfo.Def == Defs.DamageDefOf.LC_BlackDamage) damageType = FourColorDamageType.Black;
            else if (dinfo.Def == Defs.DamageDefOf.LC_PaleDamage) damageType = FourColorDamageType.Pale;

            dinfo.SetAmount(dinfo.Amount * ResistanceFor(damageType)
                * Util.DamageUtils.GetDamageLevelFactor(attackLevel, defenseLevel));
        }
    }
}
