using LCAnomalyCore.Damage;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Misc;
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
    /// 主管四色护盾弹拦截组件：吸收对应四色（及兼容原版物理/灵能）伤害直至耗尽或超时，并在受保护单位身上绘制彩色护盾光膜特效。
    /// </summary>
    public class HediffComp_BulletShield : HediffComp, ILCPawnDamageInterceptor
    {
        private static readonly Color RedTint = new Color(0.96f, 0.28f, 0.28f, 0.85f);
        private static readonly Color WhiteTint = new Color(0.96f, 0.96f, 0.92f, 0.85f);
        private static readonly Color BlackTint = new Color(0.72f, 0.42f, 0.96f, 0.85f);
        private static readonly Color PaleTint = new Color(0.42f, 0.90f, 0.98f, 0.85f);

        private static Material matRed;
        private static Material matWhite;
        private static Material matBlack;
        private static Material matPale;

        private static Material iconMatRed;
        private static Material iconMatWhite;
        private static Material iconMatBlack;
        private static Material iconMatPale;

        private float remainingShield = -1f;
        private int lastAbsorbDamageTick = -9999;
        private Vector3 impactAngleVect = Vector3.zero;

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

        /// <summary>在受护盾保护的单位身周绘制对应四色气泡光膜与角标徽记。</summary>
        public void DrawShieldEffect(Vector3 drawLoc, int stackIndex, int totalShields)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned || RemainingShield <= 0f)
            {
                return;
            }

            Material bubbleMat = GetBubbleMaterial(Props.damageType);
            if (bubbleMat == null)
            {
                return;
            }

            float pct = Mathf.Clamp01(RemainingShield / Mathf.Max(1f, Props.maxShield));
            float time = Time.realtimeSinceStartup;
            float pulse = 0.035f * Mathf.Sin(time * 3.8f + Pawn.thingIDNumber + stackIndex * 1.3f);
            float size = Mathf.Lerp(1.22f, 1.44f, pct) + stackIndex * 0.14f + pulse;

            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor() + stackIndex * 0.01f;

            if (Find.TickManager != null)
            {
                int elapsed = Find.TickManager.TicksGame - lastAbsorbDamageTick;
                if (elapsed >= 0 && elapsed < 10)
                {
                    float flashFactor = (10 - elapsed) / 10f;
                    pos += impactAngleVect * (flashFactor * 0.06f);
                    size -= flashFactor * 0.05f;
                }
            }

            float angle = ((stackIndex % 2 == 0) ? 18f : -18f) * time + stackIndex * 45f;
            var matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(size, 1f, size));
            GenDraw.DrawMeshNowOrLater(MeshPool.plane10, matrix, bubbleMat, drawNow: false);

            Material iconMat = GetIconMaterial(Props.damageType);
            if (iconMat != null)
            {
                float badgeOffsetX = 0.44f - stackIndex * 0.24f;
                Vector3 badgePos = new Vector3(
                    drawLoc.x + badgeOffsetX,
                    AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f + stackIndex * 0.01f,
                    drawLoc.z + 0.46f);
                var badgeMatrix = Matrix4x4.TRS(badgePos, Quaternion.identity, new Vector3(0.32f, 1f, 0.32f));
                GenDraw.DrawMeshNowOrLater(MeshPool.plane10, badgeMatrix, iconMat, drawNow: false);
            }
        }

        private static Material GetBubbleMaterial(FourColorDamageType colorType)
        {
            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            switch (colorType)
            {
                case FourColorDamageType.Red:
                    return matRed ??= MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent, RedTint);
                case FourColorDamageType.White:
                    return matWhite ??= MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent, WhiteTint);
                case FourColorDamageType.Black:
                    return matBlack ??= MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent, BlackTint);
                case FourColorDamageType.Pale:
                    return matPale ??= MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent, PaleTint);
                default:
                    return matRed ??= MaterialPool.MatFrom("Other/ShieldBubble", ShaderDatabase.Transparent, RedTint);
            }
        }

        private static Material GetIconMaterial(FourColorDamageType colorType)
        {
            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            switch (colorType)
            {
                case FourColorDamageType.Red:
                    return iconMatRed ??= CreateIconMat(FourColorPopupTextures.IconRed);
                case FourColorDamageType.White:
                    return iconMatWhite ??= CreateIconMat(FourColorPopupTextures.IconWhite);
                case FourColorDamageType.Black:
                    return iconMatBlack ??= CreateIconMat(FourColorPopupTextures.IconBlack);
                case FourColorDamageType.Pale:
                    return iconMatPale ??= CreateIconMat(FourColorPopupTextures.IconPale);
                default:
                    return null;
            }
        }

        private static Material CreateIconMat(Texture2D tex)
        {
            return tex != null ? MaterialPool.MatFrom(tex, ShaderDatabase.Transparent, Color.white) : null;
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

            lastAbsorbDamageTick = Find.TickManager?.TicksGame ?? 0;
            impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);

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
            if (broken)
            {
                EffecterDefOf.Shield_Break?.SpawnAttached(pawn, pawn.MapHeld, 1.3f);
                FleckMaker.Static(pawn.TrueCenter(), pawn.Map, FleckDefOf.ExplosionFlash, 4.5f);
            }
            else
            {
                FleckMaker.Static(pawn.TrueCenter(), pawn.Map, FleckDefOf.ExplosionFlash, 2.2f);
            }
        }
    }
}
