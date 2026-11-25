using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 在 E.G.O. 武器技能/挥砍动作播放期间，为持握者的整个渲染树叠加后仰/前倾与冲刺步法偏移。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]
    public static class Patch_PawnRenderTree_EgoWeaponBodyPose
    {
        /// <summary>在构建绘制矩阵前注入无锁缓存的身体位移与倾角。</summary>
        public static void Prefix(PawnRenderTree __instance, ref PawnDrawParms parms)
        {
            if (parms.Portrait)
            {
                return;
            }

            Pawn pawn = parms.pawn ?? __instance?.pawn;
            if (pawn == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.TryGetBodyTransform(pawn, out Vector3 offset, out float leanDeg))
            {
                parms.matrix *= Matrix4x4.TRS(offset, Quaternion.AngleAxis(leanDeg, Vector3.up), Vector3.one);
            }
        }
    }

    /// <summary>
    /// 在绘制手持装备阶段拦截已注册动画处理器的 E.G.O. 武器，
    /// 接管双持/重劈/技能关键帧渲染，同时保留原版穿着服装的额外渲染。
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    public static class Patch_PawnRenderUtility_EgoWeaponAnimation
    {
        /// <summary>拦截已注册的 E.G.O. 武器渲染并委托至自定义动画处理器。</summary>
        public static bool Prefix(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || flags.HasFlag(PawnRenderFlags.Portrait))
            {
                return true;
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary == null || primary.def == null || !EgoWeaponAnimationManager.TryGetHandler(primary.def.defName, out IEgoWeaponAnimationHandler handler))
            {
                return true;
            }

            EgoWeaponAnimPlayback activeAnim = null;
            if (EgoWeaponAnimationManager.TryGetActive(pawn, out EgoWeaponAnimPlayback candidate) &&
                string.Equals(candidate.WeaponDefName, primary.def.defName, System.StringComparison.Ordinal))
            {
                activeAnim = candidate;
            }

            Stance_Busy stanceBusy = pawn.stances?.curStance as Stance_Busy;
            bool isAiming = !flags.HasFlag(PawnRenderFlags.NeverAimWeapon) && stanceBusy != null && !stanceBusy.neverAimWeapon && stanceBusy.focusTarg.IsValid;
            bool carryOpenly = PawnRenderUtility.CarryWeaponOpenly(pawn);

            if (activeAnim == null)
            {
                Job curJob = pawn.CurJob;
                if (curJob == null || curJob.def == null || curJob.def.neverShowWeapon)
                {
                    return true;
                }

                if (!isAiming && !carryOpenly)
                {
                    return true;
                }
            }

            float aimAngleDeg;
            if (activeAnim != null)
            {
                aimAngleDeg = activeAnim.AimAngleDeg;
            }
            else if (isAiming)
            {
                Vector3 targetVec = stanceBusy.focusTarg.HasThing ? stanceBusy.focusTarg.Thing.DrawPos : stanceBusy.focusTarg.Cell.ToVector3Shifted();
                aimAngleDeg = (targetVec - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f
                    ? (targetVec - pawn.DrawPos).AngleFlat()
                    : facing.AsAngle;

                Verb currentEffectiveVerb = pawn.CurrentEffectiveVerb;
                if (currentEffectiveVerb != null && currentEffectiveVerb.AimAngleOverride.HasValue)
                {
                    aimAngleDeg = currentEffectiveVerb.AimAngleOverride.Value;
                }
            }
            else
            {
                aimAngleDeg = facing == Rot4.West ? 270f : (facing == Rot4.East ? 90f : facing.AsAngle);
            }

            bool facingLeft = activeAnim != null
                ? activeAnim.FacingLeft
                : (Mathf.Abs(Mathf.Sin(aimAngleDeg * Mathf.Deg2Rad)) < 0.10f
                    ? facing == Rot4.West
                    : (((aimAngleDeg % 360f) + 360f) % 360f) > 180f);

            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            float elapsedTicks = activeAnim != null ? Mathf.Max(0f, ticksGame - activeAnim.StartTick) : 0f;
            float normalizedProgress = activeAnim != null ? Mathf.Clamp01(elapsedTicks / Mathf.Max(1, activeAnim.DurationTicks)) : 0f;

            EgoWeaponDrawContext ctx = new EgoWeaponDrawContext(
                pawn,
                primary,
                drawPos,
                facing,
                flags,
                aimAngleDeg,
                facingLeft,
                isAiming,
                carryOpenly,
                activeAnim,
                elapsedTicks,
                normalizedProgress);

            if (!handler.TryDrawWeapon(in ctx))
            {
                return true;
            }

            if (pawn.apparel != null)
            {
                var worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    worn[i]?.DrawWornExtras();
                }
            }

            return false;
        }
    }

    internal static class EgoWeaponMeleePatchUtility
    {
        public static bool ShouldForcePrimaryMeleeVerbs(ThingWithComps primary)
        {
            if (primary?.def == null)
            {
                return false;
            }

            if (EgoWeaponAnimationManager.TryGetHandler(primary.def.defName, out _))
            {
                return true;
            }

            return primary.def.IsMeleeWeapon && primary.def.HasModExtension<FourColorWeaponExtension>();
        }

        public static bool HasUsablePrimaryMeleeVerb(Pawn pawn, ThingWithComps primary)
        {
            List<Verb> verbs = primary?.GetComp<CompEquippable>()?.AllVerbs;
            if (verbs == null)
            {
                return false;
            }

            for (int i = 0; i < verbs.Count; i++)
            {
                Verb v = verbs[i];
                if (v != null && v.IsMeleeAttack && v.IsStillUsableBy(pawn))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 持握 E.G.O. 武器近战时屏蔽无手拳头/头部/牙齿以及 4% 地形泥沙攻击，确保 100% 使用主武器本身的攻击动作。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.GetUpdatedAvailableVerbsList))]
    public static class Patch_Pawn_MeleeVerbs_GetUpdatedAvailableVerbsList
    {
        public static void Postfix(Pawn_MeleeVerbs __instance, bool terrainTools, List<VerbEntry> __result)
        {
            Pawn pawn = __instance?.Pawn;
            if (pawn == null || pawn.MentalStateDef == MentalStateDefOf.SocialFighting || __result == null)
            {
                return;
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            if (!EgoWeaponMeleePatchUtility.ShouldForcePrimaryMeleeVerbs(primary)
                || !EgoWeaponMeleePatchUtility.HasUsablePrimaryMeleeVerb(pawn, primary))
            {
                return;
            }

            if (terrainTools)
            {
                __result.Clear();
                return;
            }

            List<Verb> primaryVerbs = new List<Verb>();
            for (int i = 0; i < __result.Count; i++)
            {
                Verb v = __result[i].verb;
                if (v != null && v.EquipmentSource == primary)
                {
                    primaryVerbs.Add(v);
                }
            }

            if (primaryVerbs.Count > 0 && primaryVerbs.Count < __result.Count)
            {
                float highestWeight = 0f;
                for (int i = 0; i < primaryVerbs.Count; i++)
                {
                    float w = VerbUtility.InitialVerbWeight(primaryVerbs[i], pawn);
                    if (w > highestWeight)
                    {
                        highestWeight = w;
                    }
                }

                __result.Clear();
                for (int i = 0; i < primaryVerbs.Count; i++)
                {
                    __result.Add(new VerbEntry(primaryVerbs[i], pawn, primaryVerbs, highestWeight));
                }
            }
        }
    }

    /// <summary>
    /// 若缓存的近战动词并非来自当前主手 E.G.O. 武器（如换装前遗留的拳脚或地形攻击），立即清空缓存强制重选。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
    public static class Patch_Pawn_MeleeVerbs_TryGetMeleeVerb
    {
        public static void Prefix(Pawn_MeleeVerbs __instance, ref Verb ___curMeleeVerb)
        {
            if (___curMeleeVerb == null)
            {
                return;
            }

            Pawn pawn = __instance?.Pawn;
            if (pawn == null || pawn.MentalStateDef == MentalStateDefOf.SocialFighting)
            {
                return;
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            if (EgoWeaponMeleePatchUtility.ShouldForcePrimaryMeleeVerbs(primary)
                && ___curMeleeVerb.EquipmentSource != primary
                && EgoWeaponMeleePatchUtility.HasUsablePrimaryMeleeVerb(pawn, primary))
            {
                ___curMeleeVerb = null;
            }
        }
    }

    /// <summary>
    /// 在 E.G.O. 武器近战出手瞬间（无论命中、未命中、被闪避、护盾吸收或击杀目标）触发专属普攻挥砍动画。
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Verb_MeleeAttack_TryCastShot
    {
        public static void Prefix(Verb_MeleeAttack __instance)
        {
            Pawn casterPawn = __instance?.CasterPawn;
            if (casterPawn == null || !casterPawn.Spawned || casterPawn.Dead
                || casterPawn.stances == null || casterPawn.stances.FullBodyBusy
                || casterPawn.MentalStateDef == MentalStateDefOf.SocialFighting)
            {
                return;
            }

            ThingWithComps primary = casterPawn.equipment?.Primary;
            if (primary == null || primary.def == null || __instance.EquipmentSource != primary)
            {
                return;
            }

            if (EgoWeaponAnimationManager.TryGetHandler(primary.def.defName, out IEgoWeaponAnimationHandler handler))
            {
                handler.OnNormalMeleeAttack(casterPawn, primary, __instance.CurrentTarget.Thing, __instance);
            }
        }
    }

    /// <summary>
    /// 当 E.G.O. 武器正在播放自定义身法/挥砍动画时，抑制原版近战抖动位移以免叠加错位。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.Notify_MeleeAttackOn))]
    public static class Patch_Pawn_DrawTracker_Notify_MeleeAttackOn
    {
        public static bool Prefix(Pawn ___pawn)
        {
            if (___pawn != null && EgoWeaponAnimationManager.IsPlaying(___pawn))
            {
                return false;
            }

            return true;
        }
    }
}
