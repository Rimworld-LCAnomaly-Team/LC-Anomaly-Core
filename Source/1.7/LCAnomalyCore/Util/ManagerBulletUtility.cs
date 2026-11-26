using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Hediffs;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.Util
{
    /// <summary>主管战术子弹类型。</summary>
    public enum EManagerBulletType
    {
        HpHeal,
        SpHeal,
        RedShield,
        WhiteShield,
        BlackShield,
        PaleShield,
        Slow,
        Execution
    }

    /// <summary>
    /// 主管战术子弹系统：由中央本部、福利部、惩戒部通电核心解锁并发射。
    /// </summary>
    public static class ManagerBulletUtility
    {
        public const float StandardBulletRadius = 3.5f;
        public const float SlowBulletRadius = 4.0f;
        public const float ExecutionBulletRadius = 1.5f;
        public const float HpBulletHealAmount = 20f;
        public const float SpBulletMentalHeal = 0.50f;
        public const int BulletRechargeIntervalTicks = 6000;
        public const int BinahBulletRechargeIntervalTicks = 4000;

        /// <summary>按 Lobotomy Corporation 原作顺序排列的 8 种战术子弹。</summary>
        public static readonly EManagerBulletType[] OrderedBulletTypes =
        {
            EManagerBulletType.HpHeal,
            EManagerBulletType.SpHeal,
            EManagerBulletType.RedShield,
            EManagerBulletType.WhiteShield,
            EManagerBulletType.BlackShield,
            EManagerBulletType.PaleShield,
            EManagerBulletType.Slow,
            EManagerBulletType.Execution
        };

        /// <summary>当前正在瞄准中的主管战术子弹类型（未处于子弹瞄准时为 null）。</summary>
        public static EManagerBulletType? ActiveTargetingBullet { get; private set; }

        /// <summary>获取指定主管战术子弹对应的图标资源路径。</summary>
        public static string GetBulletIconPath(EManagerBulletType bulletType)
        {
            switch (bulletType)
            {
                case EManagerBulletType.HpHeal: return "UI/Commands/Bullets/Bullet_HP";
                case EManagerBulletType.SpHeal: return "UI/Commands/Bullets/Bullet_SP";
                case EManagerBulletType.RedShield: return "UI/Commands/Bullets/Bullet_RedShield";
                case EManagerBulletType.WhiteShield: return "UI/Commands/Bullets/Bullet_WhiteShield";
                case EManagerBulletType.BlackShield: return "UI/Commands/Bullets/Bullet_BlackShield";
                case EManagerBulletType.PaleShield: return "UI/Commands/Bullets/Bullet_PaleShield";
                case EManagerBulletType.Slow: return "UI/Commands/Bullets/Bullet_Slow";
                case EManagerBulletType.Execution: return "UI/Commands/Bullets/Bullet_Execution";
                default: return "UI/Commands/Bullets/Bullet_HP";
            }
        }

        /// <summary>获取指定主管战术子弹的作用半径（格）。</summary>
        public static float GetBulletRadius(EManagerBulletType bulletType)
        {
            switch (bulletType)
            {
                case EManagerBulletType.Slow: return SlowBulletRadius;
                case EManagerBulletType.Execution: return ExecutionBulletRadius;
                default: return StandardBulletRadius;
            }
        }

        /// <summary>获取指定主管战术子弹对应的主题色。</summary>
        public static Color GetBulletAccentColor(EManagerBulletType bulletType)
        {
            switch (bulletType)
            {
                case EManagerBulletType.HpHeal: return new Color(0.32f, 0.92f, 0.44f);
                case EManagerBulletType.SpHeal: return new Color(0.35f, 0.86f, 0.96f);
                case EManagerBulletType.RedShield: return new Color(0.95f, 0.30f, 0.30f);
                case EManagerBulletType.WhiteShield: return new Color(0.92f, 0.94f, 0.96f);
                case EManagerBulletType.BlackShield: return new Color(0.74f, 0.45f, 0.96f);
                case EManagerBulletType.PaleShield: return new Color(0.45f, 0.90f, 0.98f);
                case EManagerBulletType.Slow: return new Color(0.42f, 0.72f, 0.98f);
                case EManagerBulletType.Execution: return new Color(0.96f, 0.22f, 0.28f);
                default: return Color.white;
            }
        }

        /// <summary>进入主管战术子弹瞄准模式并附带作用域圆环高亮预览。</summary>
        public static void BeginBulletTargeting(Map map, EManagerBulletType bulletType)
        {
            if (map == null || Find.Targeter == null || !IsBulletUnlocked(map, bulletType))
            {
                return;
            }

            if ((GameComponent_LC.Instance?.GetManagerBulletCharges(map) ?? 0) <= 0)
            {
                DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletEmpty")?.PlayOneShotOnCamera();
                Messages.Message("LC_Bullet_NoCharges".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            TargetingParameters parms = bulletType == EManagerBulletType.Execution
                ? TargetingParameters.ForAttackAny()
                : TargetingParameters.ForCell();

            ActiveTargetingBullet = bulletType;
            Find.Targeter.BeginTargeting(
                parms,
                target =>
                {
                    if (target.IsValid && target.Cell.InBounds(map))
                    {
                        FireBulletAt(map, target.Cell, bulletType, consumeCharge: true);
                    }
                },
                highlightAction: target =>
                {
                    if (target.IsValid && target.Cell.InBounds(map))
                    {
                        GenDraw.DrawRadiusRing(target.Cell, GetBulletRadius(bulletType), GetBulletAccentColor(bulletType));
                    }
                },
                targetValidator: null,
                caster: null,
                actionWhenFinished: () =>
                {
                    if (ActiveTargetingBullet == bulletType)
                    {
                        ActiveTargetingBullet = null;
                    }
                },
                mouseAttachment: ContentFinder<Texture2D>.Get(GetBulletIconPath(bulletType), reportFailure: false));
        }

        /// <summary>计算指定地图由通电部门核心及惩戒部核心抑制提供的最大战术子弹容量。</summary>
        public static int GetMaxBulletCharges(Map map)
        {
            int max = 0;
            if (DepartmentCoreUtility.HasActiveDepartmentCore(map, EDepartmentType.CentralCommandTeam)) max += 4;
            if (DepartmentCoreUtility.HasActiveDepartmentCore(map, EDepartmentType.WelfareTeam)) max += 3;
            if (DepartmentCoreUtility.HasActiveDepartmentCore(map, EDepartmentType.DisciplinaryTeam)) max += 3;
            if (max > 0 && GameComponent_LC.Instance?.HasCoreSuppression(GameComponent_LC.CoreSuppression_Gebura) == true)
            {
                max += 2;
            }
            return max;
        }

        /// <summary>检查指定类型的战术子弹是否已在当前地图解锁。</summary>
        public static bool IsBulletUnlocked(Map map, EManagerBulletType bulletType)
        {
            switch (bulletType)
            {
                case EManagerBulletType.HpHeal:
                case EManagerBulletType.SpHeal:
                case EManagerBulletType.RedShield:
                case EManagerBulletType.WhiteShield:
                case EManagerBulletType.BlackShield:
                case EManagerBulletType.PaleShield:
                    return DepartmentCoreUtility.HasActiveDepartmentCore(map, EDepartmentType.CentralCommandTeam);

                case EManagerBulletType.Slow:
                    return DepartmentCoreUtility.HasActiveDepartmentCore(map, EDepartmentType.WelfareTeam);

                case EManagerBulletType.Execution:
                    return DepartmentCoreUtility.HasActiveDepartmentCore(map, EDepartmentType.DisciplinaryTeam);

                default:
                    return false;
            }
        }

        /// <summary>为通电中的战术部门核心生成对应的主管子弹 Gizmo。</summary>
        public static IEnumerable<Gizmo> GetBulletGizmos(Building_DepartmentCore core)
        {
            if (core == null || !core.Spawned || core.Map == null || core.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (core.CompPower != null && !core.CompPower.PowerOn)
            {
                yield break;
            }

            EDepartmentType dept = core.CompDepartmentCore?.Props?.departmentType ?? EDepartmentType.Null;
            if (dept == EDepartmentType.CentralCommandTeam)
            {
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.HpHeal, "UI/Commands/Bullets/Bullet_HP");
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.SpHeal, "UI/Commands/Bullets/Bullet_SP");
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.RedShield, "UI/Commands/Bullets/Bullet_RedShield");
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.WhiteShield, "UI/Commands/Bullets/Bullet_WhiteShield");
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.BlackShield, "UI/Commands/Bullets/Bullet_BlackShield");
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.PaleShield, "UI/Commands/Bullets/Bullet_PaleShield");
            }
            else if (dept == EDepartmentType.WelfareTeam)
            {
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.Slow, "UI/Commands/Bullets/Bullet_Slow");
            }
            else if (dept == EDepartmentType.DisciplinaryTeam)
            {
                yield return BuildBulletGizmo(core.Map, EManagerBulletType.Execution, "UI/Commands/Bullets/Bullet_Execution");
            }
        }

        private static Command_Action BuildBulletGizmo(Map map, EManagerBulletType bulletType, string iconPath)
        {
            GameComponent_LC lc = GameComponent_LC.Instance;
            int charges = lc?.GetManagerBulletCharges(map) ?? 0;
            int max = GetMaxBulletCharges(map);

            string keySuffix = bulletType.ToString();
            var cmd = new Command_Action
            {
                defaultLabel = $"{"LC_Bullet_" + keySuffix + "_Label"}".Translate() + $" ({charges}/{max})",
                defaultDesc = $"{"LC_Bullet_" + keySuffix + "_Desc"}".Translate(charges, max),
                icon = ContentFinder<Texture2D>.Get(iconPath, reportFailure: false) ?? BaseContent.BadTex,
                action = delegate
                {
                    BeginBulletTargeting(map, bulletType);
                }
            };

            if (charges <= 0)
            {
                cmd.Disable("LC_Bullet_NoCharges".Translate());
            }

            return cmd;
        }

        /// <summary>在指定地图与坐标发射一枚主管战术子弹，返回是否命中并生效。</summary>
        public static bool FireBulletAt(Map map, IntVec3 targetCell, EManagerBulletType bulletType, bool consumeCharge = true)
        {
            if (map == null || !targetCell.InBounds(map) || !IsBulletUnlocked(map, bulletType))
            {
                return false;
            }

            Pawn executionVictim = null;
            if (bulletType == EManagerBulletType.Execution)
            {
                executionVictim = FindExecutionVictim(map, targetCell);
                if (executionVictim == null)
                {
                    Messages.Message("LC_Bullet_ExecutionInvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }
            }

            GameComponent_LC lc = GameComponent_LC.Instance;
            if (consumeCharge && (lc == null || !lc.TryConsumeManagerBullet(map)))
            {
                DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletEmpty")?.PlayOneShotOnCamera();
                return false;
            }

            DefDatabase<SoundDef>.GetNamedSilentFail("LC_Sound_BulletFire")?.PlayOneShotOnCamera(map);
            FleckMaker.ThrowLightningGlow(targetCell.ToVector3Shifted(), map, 1.4f);

            switch (bulletType)
            {
                case EManagerBulletType.HpHeal:
                    return ApplyHpHeal(map, targetCell);
                case EManagerBulletType.SpHeal:
                    return ApplySpHeal(map, targetCell);
                case EManagerBulletType.RedShield:
                    return ApplyShield(map, targetCell, "LC_BulletShield_Red", new Color(0.95f, 0.30f, 0.30f));
                case EManagerBulletType.WhiteShield:
                    return ApplyShield(map, targetCell, "LC_BulletShield_White", Color.white);
                case EManagerBulletType.BlackShield:
                    return ApplyShield(map, targetCell, "LC_BulletShield_Black", new Color(0.72f, 0.44f, 0.95f));
                case EManagerBulletType.PaleShield:
                    return ApplyShield(map, targetCell, "LC_BulletShield_Pale", new Color(0.45f, 0.88f, 0.98f));
                case EManagerBulletType.Slow:
                    return ApplySlow(map, targetCell);
                case EManagerBulletType.Execution:
                    return ApplyExecution(map, executionVictim);
                default:
                    return false;
            }
        }

        private static bool ApplyHpHeal(Map map, IntVec3 center)
        {
            PlayBulletSound(map, center, "LC_Sound_BulletHeal");
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, 2.5f);
            bool any = false;
            foreach (Pawn pawn in FriendlyPawnsInRadius(map, center, StandardBulletRadius))
            {
                float budget = HpBulletHealAmount;
                List<Hediff_Injury> injuries = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i => i.Severity).ToList();
                foreach (Hediff_Injury injury in injuries)
                {
                    if (budget <= 0f) break;
                    float healed = Mathf.Min(budget, injury.Severity);
                    injury.Heal(healed);
                    budget -= healed;
                    any = true;
                }

                MoteMaker.ThrowText(pawn.DrawPos, map, "HP+", Color.green);
                FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.HealingCross, 0.42f);
                any = true;
            }
            return any;
        }

        private static bool ApplySpHeal(Map map, IntVec3 center)
        {
            PlayBulletSound(map, center, "LC_Sound_BulletHeal");
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, 2.5f);
            bool any = false;
            foreach (Pawn pawn in FriendlyPawnsInRadius(map, center, StandardBulletRadius))
            {
                Hediff mental = pawn.health?.hediffSet?.GetFirstHediffOfDef(Defs.HediffDefOf.LC_MentalDamage);
                if (mental != null)
                {
                    mental.Severity -= SpBulletMentalHeal;
                    if (mental.Severity <= 0.001f)
                    {
                        pawn.health.RemoveHediff(mental);
                    }
                }

                if (pawn.InMentalState && pawn.MentalStateDef != null && !pawn.MentalStateDef.IsAggro)
                {
                    pawn.MentalState?.RecoverFromState();
                }

                MoteMaker.ThrowText(pawn.DrawPos, map, "SP+", Color.cyan);
                FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.HealingCross, 0.42f);
                any = true;
            }
            return any;
        }

        private static bool ApplyShield(Map map, IntVec3 center, string shieldHediffDefName, Color color)
        {
            PlayBulletSound(map, center, "LC_Sound_BulletShield");
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, 2.5f);
            HediffDef shieldDef = DefDatabase<HediffDef>.GetNamedSilentFail(shieldHediffDefName);
            if (shieldDef == null)
            {
                return false;
            }

            bool any = false;
            foreach (Pawn pawn in FriendlyPawnsInRadius(map, center, StandardBulletRadius))
            {
                Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(shieldDef);
                if (existing == null)
                {
                    existing = pawn.health?.AddHediff(shieldDef);
                }

                existing?.TryGetComp<HediffComp_BulletShield>()?.RefreshShield();
                MoteMaker.ThrowText(pawn.DrawPos, map, shieldDef.LabelCap, color);
                any = true;
            }
            return any;
        }

        private static bool ApplySlow(Map map, IntVec3 center)
        {
            PlayBulletSound(map, center, "LC_Sound_BulletSlow");
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, 3.0f);
            HediffDef slowDef = DefDatabase<HediffDef>.GetNamedSilentFail("LC_BulletSlow");
            if (slowDef == null)
            {
                return false;
            }

            bool any = false;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned) continue;
                if (!pawn.Position.InHorDistOf(center, SlowBulletRadius)) continue;
                if (pawn.Faction == Faction.OfPlayer) continue;

                Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(slowDef);
                if (existing == null)
                {
                    existing = pawn.health?.AddHediff(slowDef);
                }
                else
                {
                    HediffComp_Disappears disappears = existing.TryGetComp<HediffComp_Disappears>();
                    if (disappears != null)
                    {
                        disappears.ticksToDisappear = disappears.Props.disappearsAfterTicks.max;
                    }
                }

                MoteMaker.ThrowText(pawn.DrawPos, map, "SLOW", new Color(0.45f, 0.72f, 0.94f));
                any = true;
            }
            return any;
        }

        private static Pawn FindExecutionVictim(Map map, IntVec3 center)
        {
            return map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned
                    && p.Position.InHorDistOf(center, ExecutionBulletRadius)
                    && p.RaceProps != null && p.RaceProps.Humanlike
                    && p.TryGetComp<CompAbnormality>() == null)
                .OrderBy(p => p.Position.DistanceToSquared(center))
                .FirstOrDefault();
        }

        private static bool ApplyExecution(Map map, Pawn victim)
        {
            if (victim == null)
            {
                Messages.Message("LC_Bullet_ExecutionInvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            PlayBulletSound(map, victim.Position, "LC_Sound_BulletExecution");
            FleckMaker.ThrowMicroSparks(victim.DrawPos, map);
            MoteMaker.ThrowText(victim.DrawPos, map, "LC_Bullet_ExecutedTag".Translate(), new Color(0.95f, 0.20f, 0.25f));
            // 原作处决弹：直接抹除目标（不留尸体、不触发殖民者死亡士气惩罚）
            victim.Destroy(DestroyMode.Vanish);
            return true;
        }

        private static IEnumerable<Pawn> FriendlyPawnsInRadius(Map map, IntVec3 center, float radius)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned) continue;
                if (pawn.Faction != Faction.OfPlayer) continue;
                if (!pawn.Position.InHorDistOf(center, radius)) continue;
                yield return pawn;
            }
        }

        private static void PlayBulletSound(Map map, IntVec3 cell, string soundDefName)
        {
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
            sound?.PlayOneShot(new TargetInfo(cell, map));
        }
    }
}
