using Verse;

namespace LCAnomalyLibrary.Comp.Concrete
{
    public class CompEgoSuit : ThingComp
    {
        public CompPoperties_EgoSuit Props => (CompPoperties_EgoSuit)props;

        public void ApplyEGOResist(ref DamageInfo dinfo)
        {
            //武器等级
            CompEgoWeapon compWeapon = ((Pawn)dinfo.Instigator).equipment.bondedWeapon.TryGetComp<CompEgoWeapon>();
            int levelWeapon = compWeapon != null ? Util.DamageUtils.LevelTag2Int(compWeapon.Props.level) : 1;
            //护甲等级
            int levelSelf = Util.DamageUtils.LevelTag2Int(Props.level);
            //压制等级
            float levelFactor = Util.DamageUtils.GetDamageLevelFactor(levelWeapon, levelSelf);

            //计算基础
            float damageBase = 0;
            if (dinfo.Def == Defs.DamageDefOf.LC_RedDamage)
                damageBase = dinfo.Amount * Props.redResist;
            else if (dinfo.Def == Defs.DamageDefOf.LC_WhiteDamage)
                damageBase = dinfo.Amount * Props.whiteResist;
            else if (dinfo.Def == Defs.DamageDefOf.LC_BlackDamage)
                damageBase = dinfo.Amount * Props.blackResist;
            else if (dinfo.Def == Defs.DamageDefOf.LC_PaleDamage)
                damageBase = dinfo.Amount * Props.paleResist;

            //更改伤害量
            dinfo.SetAmount(damageBase * levelFactor);
        }
    }
}