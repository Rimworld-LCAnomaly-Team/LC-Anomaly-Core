using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAnomalyCore.Patch
{
    /// <summary>
    /// 为所有 E.G.O. 武器主动技能提供图标右下角同格自动施放复选框，并在战斗中于冷却就绪时自动触发技能。
    /// </summary>
    public static class EgoWeaponAutoCastUtility
    {
        private static readonly AccessTools.FieldRef<Command_Ability, List<Command_Ability>> GroupedCastsRef =
            AccessTools.FieldRefAccess<Command_Ability, List<Command_Ability>>("groupedCasts");

        private static bool consumedCheckboxMouseDown;

        /// <summary>判断技能是否挂载在 E.G.O. 主武器上并返回该武器实例。</summary>
        public static bool TryGetEgoWeaponForAbility(Ability ability, out ThingWithComps weapon)
        {
            ThingWithComps primary = ability?.pawn?.equipment?.Primary;
            if (primary?.def == null)
            {
                weapon = null;
                return false;
            }

            CompEquippableAbility eqAbility = primary.GetComp<CompEquippableAbility>();
            if (eqAbility == null || eqAbility.AbilityForReading != ability)
            {
                weapon = null;
                return false;
            }

            if (!EgoWeaponAnimationManager.TryGetHandler(primary.def.defName, out _)
                && !primary.def.HasModExtension<FourColorWeaponExtension>())
            {
                weapon = null;
                return false;
            }

            weapon = primary;
            return true;
        }

        /// <summary>判断命令格是否应显示右下角 E.G.O. 自动施放勾选框。</summary>
        public static bool IsEligibleCommand(Command_Ability command)
        {
            return command != null
                && !command.devGizmo
                && command.Ability != null
                && command.Ability.pawn?.Faction == Faction.OfPlayer
                && TryGetEgoWeaponForAbility(command.Ability, out _);
        }

        /// <summary>计算技能格右下角复选框区域。</summary>
        public static Rect GetCheckboxRect(Rect butRect, bool shrunk)
        {
            float size = shrunk ? 14f : 20f;
            float margin = shrunk ? 1f : 2f;
            return new Rect(butRect.xMax - size - margin, butRect.yMax - size - margin, size, size);
        }

        /// <summary>在 Gizmo 按钮点击检测前拦截右下角勾选框鼠标事件，避免误触发技能瞄准。</summary>
        public static void HandleCheckboxInput(Command_Ability command, Rect butRect, bool shrunk)
        {
            if (!IsEligibleCommand(command))
            {
                return;
            }

            Event ev = Event.current;
            if (ev == null)
            {
                return;
            }

            Rect checkboxRect = GetCheckboxRect(butRect, shrunk);
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (Mouse.IsOver(checkboxRect))
                {
                    ToggleCommand(command);
                    consumedCheckboxMouseDown = true;
                    ev.Use();
                }
                else
                {
                    consumedCheckboxMouseDown = false;
                }
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && consumedCheckboxMouseDown)
            {
                consumedCheckboxMouseDown = false;
                ev.Use();
            }
        }

        /// <summary>在技能格（含冷却遮罩与提示层）上方绘制右下角自动施放复选框及专属悬停提示。</summary>
        public static void DrawAutoCastCheckbox(Command_Ability command, Rect butRect, bool shrunk)
        {
            if (!IsEligibleCommand(command))
            {
                return;
            }

            Rect checkboxRect = GetCheckboxRect(butRect, shrunk);
            bool hovered = Mouse.IsOver(checkboxRect);
            MultiCheckboxState state = GetCheckboxState(command);

            Color prevColor = GUI.color;
            GUI.color = Color.white;

            Color borderColor = hovered ? Color.white : new Color(0.58f, 0.61f, 0.66f, 0.88f);
            Widgets.DrawBoxSolidWithOutline(checkboxRect, new Color(0.08f, 0.09f, 0.11f, 0.82f), borderColor);

            Texture2D tex = state == MultiCheckboxState.On
                ? Widgets.CheckboxOnTex
                : (state == MultiCheckboxState.Partial ? Widgets.CheckboxPartialTex : Widgets.CheckboxOffTex);
            GUI.DrawTexture(checkboxRect.ContractedBy(1f), tex);

            if (hovered)
            {
                Widgets.DrawHighlight(checkboxRect);
                MouseoverSounds.DoRegion(checkboxRect);
                TooltipHandler.ClearTooltipsFrom(checkboxRect);

                string stateKey = state == MultiCheckboxState.On
                    ? "LC_EgoAutoCast_StateOn"
                    : (state == MultiCheckboxState.Partial ? "LC_EgoAutoCast_StatePartial" : "LC_EgoAutoCast_StateOff");
                string tipText = "LC_EgoAutoCast_TooltipTitle".Translate() + ": " + stateKey.Translate()
                    + "\n\n" + "LC_EgoAutoCast_TooltipDesc".Translate();
                int tipId = Gen.HashCombineInt(741852963, command.Ability.Id);
                TooltipHandler.TipRegion(checkboxRect, new TipSignal(tipText, tipId));
            }

            GUI.color = prevColor;
        }

        /// <summary>检查持握者当前是否满足自动施放已启用 E.G.O. 武器主动技能的自身状态条件。</summary>
        public static bool TryGetReadyAutoCastAbility(Pawn pawn, out Ability ability, out ThingWithComps weapon)
        {
            ability = null;
            weapon = null;

            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return false;
            }

            if (pawn.Faction != Faction.OfPlayer || pawn.InMentalState || pawn.IsCarryingPawn())
            {
                return false;
            }

            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.stances == null || pawn.stances.FullBodyBusy)
            {
                return false;
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            CompEquippableAbility eqAbility = primary?.GetComp<CompEquippableAbility>();
            Ability candidate = eqAbility?.AbilityForReading;
            if (!TryGetEgoWeaponForAbility(candidate, out ThingWithComps egoWeapon))
            {
                return false;
            }

            GameComponent_LC gc = GameComponent_LC.Instance;
            if (gc == null || !gc.IsEgoWeaponAutoCastEnabled(egoWeapon))
            {
                return false;
            }

            if (candidate.verb == null || candidate.verb.state == VerbState.Bursting
                || !candidate.CanCast || candidate.OnCooldown || candidate.Casting)
            {
                return false;
            }

            ability = candidate;
            weapon = egoWeapon;
            return true;
        }

        /// <summary>校验目标是否为可合法自动施放技能的活体敌人且在射程/贴身与视线内。</summary>
        public static bool IsValidAutoCastTarget(Pawn caster, Ability ability, Thing target, bool requireActiveThreat)
        {
            if (caster == null || ability?.verb == null || !(target is Pawn targetPawn))
            {
                return false;
            }

            if (targetPawn == caster || !targetPawn.Spawned || targetPawn.Map != caster.Map
                || targetPawn.Dead || targetPawn.Downed || targetPawn.IsPsychologicallyInvisible())
            {
                return false;
            }

            if (caster.Faction != null && targetPawn.Faction != null && !targetPawn.HostileTo(caster))
            {
                if (targetPawn.Faction == caster.Faction || targetPawn.Faction.RelationKindWith(caster.Faction) == FactionRelationKind.Ally)
                {
                    return false;
                }
            }

            if (requireActiveThreat)
            {
                if (!caster.HostileTo(targetPawn)
                    || targetPawn.ThreatDisabled(caster)
                    || !GenHostility.IsActiveThreatTo(targetPawn, caster.Faction, ignoreHives: false))
                {
                    return false;
                }
            }

            LocalTargetInfo targInfo = new LocalTargetInfo(targetPawn);
            Verb verb = ability.verb;
            if (verb is Verb_CastAbilityTouch || verb.EffectiveRange <= 1.42f)
            {
                if (!caster.CanReachImmediate(targetPawn, PathEndMode.Touch))
                {
                    return false;
                }
            }
            else if (!verb.CanHitTargetFrom(caster.Position, targInfo))
            {
                return false;
            }

            return ability.CanApplyOn(targInfo) && verb.ValidateTarget(targInfo, showMessages: false);
        }

        /// <summary>在不打断当前战斗 Job 的前提下尝试立即启动 E.G.O. 主动技能施放（含前摇与通知）。</summary>
        public static bool TryExecuteAutoCast(Pawn caster, Ability ability, Thing target, bool requireActiveThreat)
        {
            if (caster == null || ability?.verb == null)
            {
                return false;
            }

            LocalTargetInfo castTarget;
            if (!ability.def.targetRequired)
            {
                castTarget = new LocalTargetInfo(caster);
            }
            else
            {
                if (!IsValidAutoCastTarget(caster, ability, target, requireActiveThreat))
                {
                    return false;
                }

                castTarget = new LocalTargetInfo(target);
            }

            bool started = ability.verb.TryStartCastOn(
                castTarget,
                LocalTargetInfo.Invalid,
                surpriseAttack: false,
                canHitNonTargetPawns: false);
            if (!started)
            {
                return false;
            }

            ability.Notify_StartedCasting();
            caster.pather?.StopDead();
            return true;
        }

        /// <summary>在持握者待命戒备（Wait_Combat）时扫描技能有效射程内最近的主动敌对目标。</summary>
        public static Pawn FindBestAutoCastTargetInRange(Pawn caster, Ability ability)
        {
            if (caster == null || caster.Map == null || ability?.verb == null)
            {
                return null;
            }

            Verb verb = ability.verb;
            Pawn bestTarget = null;
            float bestDistSq = float.MaxValue;

            if (verb is Verb_CastAbilityTouch || verb.EffectiveRange <= 1.42f)
            {
                for (int i = 0; i < 9; i++)
                {
                    IntVec3 cell = caster.Position + GenAdj.AdjacentCellsAndInside[i];
                    if (!cell.InBounds(caster.Map))
                    {
                        continue;
                    }

                    List<Thing> things = cell.GetThingList(caster.Map);
                    for (int j = 0; j < things.Count; j++)
                    {
                        if (things[j] is Pawn candidate && IsValidAutoCastTarget(caster, ability, candidate, requireActiveThreat: true))
                        {
                            float distSq = (candidate.Position - caster.Position).LengthHorizontalSquared;
                            if (distSq < bestDistSq)
                            {
                                bestDistSq = distSq;
                                bestTarget = candidate;
                            }
                        }
                    }
                }

                return bestTarget;
            }

            float maxRange = verb.EffectiveRange;
            float maxDistSq = maxRange * maxRange;
            List<IAttackTarget> potentialTargets = caster.Map.attackTargetsCache.GetPotentialTargetsFor(caster);
            for (int i = 0; i < potentialTargets.Count; i++)
            {
                if (potentialTargets[i]?.Thing is not Pawn candidate)
                {
                    continue;
                }

                float distSq = (candidate.Position - caster.Position).LengthHorizontalSquared;
                if (distSq > maxDistSq || distSq >= bestDistSq)
                {
                    continue;
                }

                if (IsValidAutoCastTarget(caster, ability, candidate, requireActiveThreat: true))
                {
                    bestDistSq = distSq;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private static void CollectTargetWeapons(Command_Ability command, List<ThingWithComps> outWeapons)
        {
            outWeapons.Clear();
            List<Command_Ability> grouped = GroupedCastsRef(command);
            if (grouped != null && grouped.Count > 0)
            {
                for (int i = 0; i < grouped.Count; i++)
                {
                    Command_Ability item = grouped[i];
                    if (item != null && TryGetEgoWeaponForAbility(item.Ability, out ThingWithComps w) && !outWeapons.Contains(w))
                    {
                        outWeapons.Add(w);
                    }
                }
            }

            if (outWeapons.Count == 0 && TryGetEgoWeaponForAbility(command.Ability, out ThingWithComps single))
            {
                outWeapons.Add(single);
            }
        }

        private static MultiCheckboxState GetCheckboxState(Command_Ability command)
        {
            GameComponent_LC gc = GameComponent_LC.Instance;
            if (gc == null)
            {
                return MultiCheckboxState.Off;
            }

            List<ThingWithComps> weapons = new List<ThingWithComps>();
            CollectTargetWeapons(command, weapons);
            if (weapons.Count == 0)
            {
                return MultiCheckboxState.Off;
            }

            bool anyOn = false;
            bool anyOff = false;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (gc.IsEgoWeaponAutoCastEnabled(weapons[i]))
                {
                    anyOn = true;
                }
                else
                {
                    anyOff = true;
                }
            }

            if (anyOn && anyOff)
            {
                return MultiCheckboxState.Partial;
            }

            return anyOn ? MultiCheckboxState.On : MultiCheckboxState.Off;
        }

        private static void ToggleCommand(Command_Ability command)
        {
            GameComponent_LC gc = GameComponent_LC.Instance;
            if (gc == null)
            {
                return;
            }

            List<ThingWithComps> weapons = new List<ThingWithComps>();
            CollectTargetWeapons(command, weapons);
            if (weapons.Count == 0)
            {
                return;
            }

            bool enable = GetCheckboxState(command) != MultiCheckboxState.On;
            for (int i = 0; i < weapons.Count; i++)
            {
                gc.SetEgoWeaponAutoCast(weapons[i], enable);
            }

            SoundDef sound = enable ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff;
            sound?.PlayOneShotOnCamera();
        }
    }

    /// <summary>在主按钮检测前处理 E.G.O. 技能格右下角勾选框点击事件。</summary>
    [HarmonyPatch(typeof(Command_Ability), "GizmoOnGUIInt")]
    public static class Patch_Command_Ability_GizmoOnGUIInt
    {
        public static void Prefix(Command_Ability __instance, Rect butRect, GizmoRenderParms parms)
        {
            EgoWeaponAutoCastUtility.HandleCheckboxInput(__instance, butRect, parms.shrunk);
        }
    }

    /// <summary>在标准尺寸的 E.G.O. 技能格（冷却遮罩之上）绘制右下角自动施放复选框。</summary>
    [HarmonyPatch(typeof(Command_Ability), nameof(Command_Ability.GizmoOnGUI))]
    public static class Patch_Command_Ability_GizmoOnGUI
    {
        public static void Postfix(Command_Ability __instance, Vector2 topLeft, float maxWidth)
        {
            Rect butRect = new Rect(topLeft.x, topLeft.y, __instance.GetWidth(maxWidth), 75f);
            EgoWeaponAutoCastUtility.DrawAutoCastCheckbox(__instance, butRect, shrunk: false);
        }
    }

    /// <summary>在缩小尺寸的 E.G.O. 技能格上方绘制紧凑右下角自动施放复选框。</summary>
    [HarmonyPatch(typeof(Command), nameof(Command.GizmoOnGUIShrunk))]
    public static class Patch_Command_GizmoOnGUIShrunk
    {
        public static void Postfix(Command __instance, Vector2 topLeft, float size)
        {
            if (__instance is Command_Ability cmdAbility)
            {
                EgoWeaponAutoCastUtility.DrawAutoCastCheckbox(cmdAbility, new Rect(topLeft.x, topLeft.y, size, size), shrunk: true);
            }
        }
    }

    /// <summary>在远程静止射击或待命接敌即将开火时，优先自动施放就绪的 E.G.O. 主动技能。</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
    public static class Patch_Pawn_TryStartAttack_EgoAutoCast
    {
        public static bool Prefix(Pawn __instance, LocalTargetInfo targ, ref bool __result)
        {
            JobDef curJobDef = __instance?.CurJobDef;
            if (curJobDef != JobDefOf.AttackStatic && curJobDef != JobDefOf.Wait_Combat)
            {
                return true;
            }

            if (!EgoWeaponAutoCastUtility.TryGetReadyAutoCastAbility(__instance, out Ability ability, out _))
            {
                return true;
            }

            bool requireActiveThreat = curJobDef == JobDefOf.Wait_Combat;
            if (EgoWeaponAutoCastUtility.TryExecuteAutoCast(__instance, ability, targ.Thing, requireActiveThreat))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    /// <summary>在近战贴身出刀瞬间，若开启了自动施放且技能就绪则优先施放 E.G.O. 主动技能。</summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    public static class Patch_Pawn_MeleeVerbs_TryMeleeAttack_EgoAutoCast
    {
        public static bool Prefix(Pawn_MeleeVerbs __instance, Thing target, ref bool __result)
        {
            Pawn pawn = __instance?.Pawn;
            if (pawn == null || pawn.MentalStateDef == MentalStateDefOf.SocialFighting)
            {
                return true;
            }

            JobDef curJobDef = pawn.CurJobDef;
            if (curJobDef != JobDefOf.AttackMelee && curJobDef != JobDefOf.Wait_Combat)
            {
                return true;
            }

            if (!EgoWeaponAutoCastUtility.TryGetReadyAutoCastAbility(pawn, out Ability ability, out _))
            {
                return true;
            }

            bool requireActiveThreat = curJobDef == JobDefOf.Wait_Combat;
            if (EgoWeaponAutoCastUtility.TryExecuteAutoCast(pawn, ability, target, requireActiveThreat))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    /// <summary>每 15 Tick 轮询战斗中（追击或征召自由开火待命）的近战 E.G.O. 持握者，在进入中距离技能射程时自动释放。</summary>
    [HarmonyPatch(typeof(Pawn_AbilityTracker), nameof(Pawn_AbilityTracker.AbilitiesTick))]
    public static class Patch_Pawn_AbilityTracker_AbilitiesTick_EgoAutoCast
    {
        public static void Postfix(Pawn_AbilityTracker __instance)
        {
            Pawn pawn = __instance?.pawn;
            if (pawn == null || !pawn.IsHashIntervalTick(15))
            {
                return;
            }

            if (!EgoWeaponAutoCastUtility.TryGetReadyAutoCastAbility(pawn, out Ability ability, out _))
            {
                return;
            }

            JobDef curJobDef = pawn.CurJobDef;
            if (curJobDef == JobDefOf.AttackMelee || curJobDef == JobDefOf.AttackStatic)
            {
                EgoWeaponAutoCastUtility.TryExecuteAutoCast(pawn, ability, pawn.CurJob?.targetA.Thing, requireActiveThreat: false);
            }
            else if (curJobDef == JobDefOf.Wait_Combat
                && pawn.Drafted
                && (pawn.drafter == null || pawn.drafter.FireAtWill)
                && (pawn.pather == null || !pawn.pather.Moving))
            {
                Pawn bestTarget = EgoWeaponAutoCastUtility.FindBestAutoCastTargetInRange(pawn, ability);
                if (bestTarget != null)
                {
                    EgoWeaponAutoCastUtility.TryExecuteAutoCast(pawn, ability, bestTarget, requireActiveThreat: true);
                }
            }
        }
    }
}
