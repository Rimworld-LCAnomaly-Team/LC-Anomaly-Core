using LCAnomalyCore.Damage;
using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.Hediffs
{
    /// <summary>主管四色护盾弹 HediffComp 属性配置。</summary>
    public class HediffCompProperties_BulletShield : HediffCompProperties
    {
        /// <summary>该护盾可吸收的四色伤害类型。</summary>
        public FourColorDamageType damageType = FourColorDamageType.Red;

        /// <summary>最大护盾吸收量。</summary>
        public float maxShield = 50f;

        /// <summary>初始化 <c>HediffCompProperties_BulletShield</c> 实例。</summary>
        public HediffCompProperties_BulletShield()
        {
            compClass = typeof(HediffComp_BulletShield);
        }
    }

    /// <summary>
    /// 主管四色护盾弹拦截组件：吸收对应四色（及兼容原版物理/灵能）伤害直至耗尽或超时。
    /// </summary>
    public class HediffComp_BulletShield : HediffComp, ILCPawnDamageInterceptor
    {
        private float remainingShield = -1f;

        /// <summary>获取配置属性。</summary>
        public HediffCompProperties_BulletShield Props => (HediffCompProperties_BulletShield)props;

        /// <summary>当前剩余护盾值。</summary>
        public float RemainingShield
        {
            get
            {
                if (remainingShield < 0f)
                {
                    remainingShield = Props.maxShield;
                }

                return remainingShield;
            }
        }

        /// <inheritdoc />
        public override void CompPostMake()
        {
            base.CompPostMake();
            remainingShield = Props.maxShield;
        }

        /// <summary>将护盾值重置为上限并刷新持续时间。</summary>
        public void RefreshShield()
        {
            remainingShield = Props.maxShield;
            HediffComp_Disappears disappears = parent?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = disappears.Props.disappearsAfterTicks.max;
            }
        }

        /// <inheritdoc />
        public override string CompLabelInBracketsExtra => $"{Mathf.CeilToInt(RemainingShield)}/{Mathf.CeilToInt(Props.maxShield)}";

        /// <inheritdoc />
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref remainingShield, "lcBulletRemainingShield", Props?.maxShield ?? 50f);
        }

        /// <inheritdoc />
        public bool TryInterceptDamage(Pawn pawn, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (pawn == null || dinfo.Amount <= 0f || RemainingShield <= 0f)
            {
                return false;
            }

            if (!MatchesShieldColor(dinfo.Def, Props.damageType))
            {
                return false;
            }

            float incoming = dinfo.Amount;
            if (RemainingShield >= incoming)
            {
                remainingShield -= incoming;
                absorbed = true;
                PlayShieldFeedback(pawn, broken: remainingShield <= 0.01f);
                if (remainingShield <= 0.01f)
                {
                    pawn.health?.RemoveHediff(parent);
                }

                return true;
            }

            // 护盾吸收部分伤害后破碎，保留溢出伤害继续生效
            float leftover = incoming - remainingShield;
            remainingShield = 0f;
            dinfo.SetAmount(leftover);
            PlayShieldFeedback(pawn, broken: true);
            pawn.health?.RemoveHediff(parent);
            return false;
        }

        private static bool MatchesShieldColor(DamageDef damageDef, FourColorDamageType shieldColor)
        {
            if (damageDef == null)
            {
                return false;
            }

            FourColorDamageExtension ext = damageDef.GetModExtension<FourColorDamageExtension>();
            if (ext != null)
            {
                return ext.damageType == shieldColor;
            }

            // 兼容原版非四色伤害：灵能按 WHITE 吸收，常规物理（锐器/钝器/枪弹/爆炸）按 RED 吸收
            if (damageDef == RimWorld.DamageDefOf.Psychic)
            {
                return shieldColor == FourColorDamageType.White;
            }

            if (shieldColor == FourColorDamageType.Red && damageDef.harmsHealth)
            {
                return true;
            }

            return false;
        }

        private static void PlayShieldFeedback(Pawn pawn, bool broken)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            SoundDef snd = DefDatabase<SoundDef>.GetNamedSilentFail(broken ? "LC_Sound_ShieldBroken" : "LC_Sound_ShieldHit");
            snd?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);
        }
    }
}
