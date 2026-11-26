using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Defs;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyCore.GameComponent
{
    /// <summary>
    /// 异想体提取深度档位。
    /// </summary>
    public enum LCExtractionTier
    {
        /// <summary>常规协议（ZAYIN / TETH / HE 均衡）</summary>
        Standard = 0,
        /// <summary>浅层共鸣（ZAYIN / TETH）</summary>
        Shallow = 1,
        /// <summary>中层共鸣（TETH / HE）</summary>
        Mid = 2,
        /// <summary>深层共鸣（HE / WAW）</summary>
        Deep = 3,
        /// <summary>深渊共振（WAW / 必含 ALEPH）</summary>
        Abyss = 4
    }

    /// <summary>
    /// 地图级异想体三选一提取管理器。
    /// 负责候选池抽取、防SL持久化候选队列、免费首抽与资源扣除、冷却计时与收容匣生成。
    /// </summary>
    public class MapComponent_LCExtraction : Verse.MapComponent
    {
        /// <summary>每次提取完成后的协议冷却时间（6 游戏小时 = 15000 Tick）。</summary>
        public const int ExtractionCooldownTicks = 15000;

        /// <summary>重抽消耗 Cogito 数量。</summary>
        public const int RerollCogitoCost = 1;

        /// <summary>重抽消耗脑啡肽箱数量。</summary>
        public const int RerollEnkephalinCost = 10;

        private int totalExtractions;
        private int lastConfirmTick = -999999;
        private bool hasPendingDraw;
        private LCExtractionTier activeTier = LCExtractionTier.Standard;
        private bool usedReroll;
        private List<ThingDef> pendingCandidates = new List<ThingDef>();
        private int selectedIndex = -1;

        /// <summary>
        /// 构造地图级提取管理器。
        /// </summary>
        /// <param name="map">所属地图。</param>
        public MapComponent_LCExtraction(Map map) : base(map)
        {
        }

        /// <summary>累计已完成提取次数。</summary>
        public int TotalExtractions => totalExtractions;

        /// <summary>是否处于新殖民地首抽免费特许期。</summary>
        public bool IsFreeCharterAvailable => totalExtractions == 0;

        /// <summary>当前是否存在尚未确认选定的三选一候选池。</summary>
        public bool HasPendingDraw => hasPendingDraw && pendingCandidates != null && pendingCandidates.Count > 0;

        /// <summary>当前挂起抽卡对应的深度档位。</summary>
        public LCExtractionTier ActiveTier => activeTier;

        /// <summary>本轮抽卡是否已使用过一次重抽机会。</summary>
        public bool UsedReroll => usedReroll;

        /// <summary>当前挂起的 3 名候选异想体 ThingDef 列表。</summary>
        public IReadOnlyList<ThingDef> PendingCandidates => pendingCandidates;

        /// <summary>当前UI中选中的候选卡片索引（0..2，未选为 -1）。</summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set => selectedIndex = value;
        }

        /// <summary>任一异想体提取确认完成事件（地图、提取档位、选定定义、具现化收容匣）。</summary>
        public static event Action<Map, LCExtractionTier, ThingDef, Thing> OnAnyAbnormalityExtracted;

        /// <summary>当前地图是否因研发部核心通电或 Binah 核心抑制而享受每轮免费“二次提取”。</summary>
        public bool IsFreeRerollAvailable => DepartmentCoreUtility.HasFreeAbnormalityReExtract(map);

        /// <summary>当前地图生效的异想体提取冷却总时长（Tick）。</summary>
        public int EffectiveCooldownTicks => DepartmentCoreUtility.GetAbnormalityExtractionCooldownTicks(map);

        /// <summary>剩余提取冷却 Tick（GodMode 下始终为 0）。</summary>
        public int CooldownTicksRemaining
        {
            get
            {
                if (DebugSettings.godMode || lastConfirmTick < 0 || Find.TickManager == null)
                {
                    return 0;
                }

                int elapsed = Find.TickManager.TicksGame - lastConfirmTick;
                return Mathf.Max(0, EffectiveCooldownTicks - elapsed);
            }
        }

        /// <summary>
        /// 获取或创建指定地图上的提取管理器组件。
        /// </summary>
        public static MapComponent_LCExtraction GetFor(Map map)
        {
            if (map == null)
            {
                return null;
            }

            MapComponent_LCExtraction comp = map.GetComponent<MapComponent_LCExtraction>();
            if (comp == null)
            {
                comp = new MapComponent_LCExtraction(map);
                map.components.Add(comp);
            }

            return comp;
        }

        /// <summary>
        /// 获取指定提取档位的 Cogito 与脑啡肽箱消耗。
        /// </summary>
        public static void GetTierCost(LCExtractionTier tier, out int cogitoCost, out int enkephalinCost)
        {
            switch (tier)
            {
                case LCExtractionTier.Shallow:
                    cogitoCost = 1;
                    enkephalinCost = 8;
                    break;
                case LCExtractionTier.Mid:
                    cogitoCost = 2;
                    enkephalinCost = 20;
                    break;
                case LCExtractionTier.Deep:
                    cogitoCost = 3;
                    enkephalinCost = 36;
                    break;
                case LCExtractionTier.Abyss:
                    cogitoCost = 5;
                    enkephalinCost = 60;
                    break;
                case LCExtractionTier.Standard:
                default:
                    cogitoCost = 1;
                    enkephalinCost = 12;
                    break;
            }
        }

        /// <summary>
        /// 该档位当前是否可适用首抽免费特许（仅常规与浅层）。
        /// </summary>
        public bool IsFreeForTier(LCExtractionTier tier)
        {
            return IsFreeCharterAvailable && (tier == LCExtractionTier.Standard || tier == LCExtractionTier.Shallow);
        }

        /// <summary>
        /// 获取当前地图上可通电运行并归属玩家的提取终端（优先返回提取井，其次 Cogito 水桶）。
        /// </summary>
        public Building FindOperationalTerminal()
        {
            if (map?.listerBuildings == null)
            {
                return null;
            }

            if (Defs.ThingDefOf.LC_ExtractionWell != null)
            {
                List<Building> wells = map.listerBuildings.AllBuildingsColonistOfDef(Defs.ThingDefOf.LC_ExtractionWell);
                for (int i = 0; i < wells.Count; i++)
                {
                    if (IsTerminalOperational(wells[i]))
                    {
                        return wells[i];
                    }
                }
            }

            if (Defs.ThingDefOf.CogitoBucket != null)
            {
                List<Building> buckets = map.listerBuildings.AllBuildingsColonistOfDef(Defs.ThingDefOf.CogitoBucket);
                for (int i = 0; i < buckets.Count; i++)
                {
                    if (IsTerminalOperational(buckets[i]))
                    {
                        return buckets[i];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取地图上任意已建成的提取终端（即使尚未通电）。
        /// </summary>
        public Building FindAnyBuiltTerminal()
        {
            if (map?.listerBuildings == null)
            {
                return null;
            }

            if (Defs.ThingDefOf.LC_ExtractionWell != null)
            {
                List<Building> wells = map.listerBuildings.AllBuildingsColonistOfDef(Defs.ThingDefOf.LC_ExtractionWell);
                if (wells.Count > 0)
                {
                    return wells[0];
                }
            }

            if (Defs.ThingDefOf.CogitoBucket != null)
            {
                List<Building> buckets = map.listerBuildings.AllBuildingsColonistOfDef(Defs.ThingDefOf.CogitoBucket);
                if (buckets.Count > 0)
                {
                    return buckets[0];
                }
            }

            return null;
        }

        /// <summary>
        /// 判断指定终端建筑当前是否处于可运行状态。
        /// </summary>
        public static bool IsTerminalOperational(Building building)
        {
            if (building == null || !building.Spawned || building.Destroyed || building.Faction != Faction.OfPlayer)
            {
                return false;
            }

            CompBreakdownable breakdown = building.GetComp<CompBreakdownable>();
            if (breakdown != null && breakdown.BrokenDown)
            {
                return false;
            }

            CompFlickable flick = building.GetComp<CompFlickable>();
            if (flick != null && !flick.SwitchIsOn)
            {
                return false;
            }

            CompPowerTrader power = building.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 统计当前地图仓储区/地面上可用的指定资源总数（跳过被禁用物品）。
        /// </summary>
        public int CountAvailableResource(ThingDef def)
        {
            if (map == null || def == null)
            {
                return 0;
            }

            int total = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t == null || t.Destroyed || !t.Spawned || t.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }

                total += t.stackCount;
            }

            return total;
        }

        /// <summary>
        /// 检查当前是否满足开始指定档位提取的条件。
        /// </summary>
        public bool CanStartExtraction(LCExtractionTier tier, out string failReason)
        {
            if (HasPendingDraw)
            {
                failReason = string.Empty;
                return true;
            }

            if (FindOperationalTerminal() == null)
            {
                failReason = FindAnyBuiltTerminal() != null
                    ? "LC_Extract_NeedPower".Translate().ToString()
                    : "LC_Extract_NeedTerminal".Translate().ToString();
                return false;
            }

            int cd = CooldownTicksRemaining;
            if (cd > 0)
            {
                failReason = "LC_Extract_OnCooldown".Translate(cd.ToStringTicksToPeriod()).ToString();
                return false;
            }

            if (DebugSettings.godMode || IsFreeForTier(tier))
            {
                failReason = string.Empty;
                return true;
            }

            GetTierCost(tier, out int cogitoCost, out int enkCost);
            int curCogito = CountAvailableResource(Defs.ThingDefOf.Cogito);
            int curEnk = CountAvailableResource(Defs.ThingDefOf.EnkephalinBox);
            if (curCogito < cogitoCost && curEnk < enkCost)
            {
                failReason = "LC_Extract_InsufficientResource".Translate(cogitoCost, enkCost, curCogito, curEnk).ToString();
                return false;
            }

            failReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 发起三选一提取：验证条件、预先扣除费用，并持久化生成 3 个不重复候选。
        /// </summary>
        public bool TryBeginExtraction(LCExtractionTier tier, out string failReason)
        {
            if (HasPendingDraw)
            {
                failReason = string.Empty;
                return true;
            }

            if (!CanStartExtraction(tier, out failReason))
            {
                return false;
            }

            List<ThingDef> rolled = RollCandidatesForTier(tier, null);
            if (rolled.Count == 0)
            {
                failReason = "LC_Extract_EmptyPool".Translate().ToString();
                return false;
            }

            if (!DebugSettings.godMode && !IsFreeForTier(tier))
            {
                GetTierCost(tier, out int cogitoCost, out int enkCost);
                if (!TryConsumeResourceOption(cogitoCost, enkCost))
                {
                    int curCogito = CountAvailableResource(Defs.ThingDefOf.Cogito);
                    int curEnk = CountAvailableResource(Defs.ThingDefOf.EnkephalinBox);
                    failReason = "LC_Extract_InsufficientResource".Translate(cogitoCost, enkCost, curCogito, curEnk).ToString();
                    return false;
                }
            }

            pendingCandidates.Clear();
            pendingCandidates.AddRange(rolled);
            activeTier = tier;
            hasPendingDraw = true;
            usedReroll = false;
            selectedIndex = -1;

            failReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 判断当前挂起抽卡是否允许执行一次重抽。
        /// </summary>
        public bool CanRerollCandidates(out string failReason)
        {
            if (!HasPendingDraw)
            {
                failReason = "LC_Extract_NoPending".Translate().ToString();
                return false;
            }

            if (usedReroll)
            {
                failReason = "LC_Extract_RerollUsed".Translate().ToString();
                return false;
            }

            if (!DebugSettings.godMode && !IsFreeRerollAvailable)
            {
                int curCogito = CountAvailableResource(Defs.ThingDefOf.Cogito);
                int curEnk = CountAvailableResource(Defs.ThingDefOf.EnkephalinBox);
                if (curCogito < RerollCogitoCost && curEnk < RerollEnkephalinCost)
                {
                    failReason = "LC_Extract_InsufficientResource".Translate(RerollCogitoCost, RerollEnkephalinCost, curCogito, curEnk).ToString();
                    return false;
                }
            }

            failReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 消耗重抽费用（1 Cogito 或 10 脑啡肽箱）并刷新本轮 3 名候选异想体。
        /// </summary>
        public bool TryRerollCandidates(out string failReason)
        {
            if (!CanRerollCandidates(out failReason))
            {
                return false;
            }

            HashSet<ThingDef> previousSet = new HashSet<ThingDef>(pendingCandidates);
            List<ThingDef> rerolled = RollCandidatesForTier(activeTier, previousSet);
            if (rerolled.Count == 0)
            {
                failReason = "LC_Extract_EmptyPool".Translate().ToString();
                return false;
            }

            if (!DebugSettings.godMode
                && !IsFreeRerollAvailable
                && !TryConsumeResourceOption(RerollCogitoCost, RerollEnkephalinCost))
            {
                int curCogito = CountAvailableResource(Defs.ThingDefOf.Cogito);
                int curEnk = CountAvailableResource(Defs.ThingDefOf.EnkephalinBox);
                failReason = "LC_Extract_InsufficientResource".Translate(RerollCogitoCost, RerollEnkephalinCost, curCogito, curEnk).ToString();
                return false;
            }

            pendingCandidates.Clear();
            pendingCandidates.AddRange(rerolled);
            usedReroll = true;
            selectedIndex = -1;

            failReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 确认提取当前选中的异想体，在提取终端旁生成对应异想体收容匣并开启冷却。
        /// </summary>
        public bool TryConfirmExtraction(Building anchorBuilding, out Thing spawnedEgg, out string failReason)
        {
            spawnedEgg = null;
            if (!HasPendingDraw)
            {
                failReason = "LC_Extract_NoPending".Translate().ToString();
                return false;
            }

            if (selectedIndex < 0 || selectedIndex >= pendingCandidates.Count)
            {
                failReason = "LC_Extract_SelectCardFirst".Translate().ToString();
                return false;
            }

            ThingDef chosenDef = pendingCandidates[selectedIndex];
            if (chosenDef == null)
            {
                failReason = "LC_Extract_EmptyPool".Translate().ToString();
                return false;
            }

            ThingDef stuff = chosenDef.MadeFromStuff ? GenStuff.DefaultStuffFor(chosenDef) : null;
            spawnedEgg = ThingMaker.MakeThing(chosenDef, stuff);
            if (spawnedEgg == null)
            {
                failReason = "LC_Extract_EmptyPool".Translate().ToString();
                return false;
            }

            Building target = (anchorBuilding != null && anchorBuilding.Spawned && anchorBuilding.Map == map)
                ? anchorBuilding
                : (FindOperationalTerminal() ?? FindAnyBuiltTerminal());

            IntVec3 spawnPos = target != null ? target.Position : map.Center;
            if (!GenPlace.TryPlaceThing(spawnedEgg, spawnPos, map, ThingPlaceMode.Near, out Thing placedThing))
            {
                IntVec3 fallbackCell = DropCellFinder.TradeDropSpot(map);
                GenPlace.TryPlaceThing(spawnedEgg, fallbackCell, map, ThingPlaceMode.Near, out placedThing);
            }

            spawnedEgg = placedThing ?? spawnedEgg;
            if (spawnedEgg != null && spawnedEgg.Spawned)
            {
                FleckMaker.ThrowLightningGlow(spawnedEgg.DrawPos, map, 1.4f);
                FleckMaker.ThrowMetaIcon(spawnedEgg.Position, map, RimWorld.FleckDefOf.PsycastAreaEffect);
                Defs.SoundDefOf.LC_Sound_Extraction_Manifest?.PlayOneShot(new TargetInfo(spawnedEgg.Position, map));
            }

            LCExtractionTier completedTier = activeTier;
            totalExtractions++;
            lastConfirmTick = Find.TickManager?.TicksGame ?? 0;
            hasPendingDraw = false;
            usedReroll = false;
            selectedIndex = -1;
            pendingCandidates.Clear();

            OnAnyAbnormalityExtracted?.Invoke(map, completedTier, chosenDef, spawnedEgg);

            string entityLabel = chosenDef.label ?? chosenDef.defName;
            AbnormalityCodexEntryDef codex = AbnormalityUtility.ResolveCodexEntry(chosenDef);
            if (codex != null)
            {
                entityLabel = codex.LabelCap.ToString();
            }

            Messages.Message(
                "LC_Extract_SuccessMsg".Translate(entityLabel),
                new LookTargets(spawnedEgg),
                MessageTypeDefOf.PositiveEvent,
                true);

            failReason = string.Empty;
            return true;
        }

        /// <summary>
        /// 开发者调试辅助：重置冷却或首抽状态。
        /// </summary>
        public void DebugResetCooldown()
        {
            lastConfirmTick = -999999;
        }

        /// <summary>
        /// 持久化保存与读取抽卡状态与冷却进度。
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref totalExtractions, "totalExtractions", 0);
            Scribe_Values.Look(ref lastConfirmTick, "lastConfirmTick", -999999);
            Scribe_Values.Look(ref hasPendingDraw, "hasPendingDraw", false);
            Scribe_Values.Look(ref activeTier, "activeTier", LCExtractionTier.Standard);
            Scribe_Values.Look(ref usedReroll, "usedReroll", false);
            Scribe_Collections.Look(ref pendingCandidates, "pendingCandidates", LookMode.Def);
            Scribe_Values.Look(ref selectedIndex, "selectedIndex", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pendingCandidates == null)
                {
                    pendingCandidates = new List<ThingDef>();
                }

                pendingCandidates.RemoveAll(d => d == null);
                if (pendingCandidates.Count == 0)
                {
                    hasPendingDraw = false;
                    selectedIndex = -1;
                }
            }
        }

        private bool TryConsumeResourceOption(int cogitoCost, int enkCost)
        {
            if (CountAvailableResource(Defs.ThingDefOf.Cogito) >= cogitoCost)
            {
                return ConsumeResource(Defs.ThingDefOf.Cogito, cogitoCost);
            }

            if (CountAvailableResource(Defs.ThingDefOf.EnkephalinBox) >= enkCost)
            {
                return ConsumeResource(Defs.ThingDefOf.EnkephalinBox, enkCost);
            }

            return false;
        }

        private bool ConsumeResource(ThingDef def, int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (CountAvailableResource(def) < amount)
            {
                return false;
            }

            int remaining = amount;
            List<Thing> things = map.listerThings.ThingsOfDef(def).ToList();
            for (int i = 0; i < things.Count && remaining > 0; i++)
            {
                Thing t = things[i];
                if (t == null || t.Destroyed || !t.Spawned || t.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }

                int take = Mathf.Min(remaining, t.stackCount);
                if (take <= 0)
                {
                    continue;
                }

                Thing taken = t.SplitOff(take);
                taken?.Destroy(DestroyMode.Vanish);
                remaining -= take;
            }

            return remaining == 0;
        }

        private HashSet<AbnormalityCodexEntryDef> GetContainedCodexSet()
        {
            HashSet<AbnormalityCodexEntryDef> contained = new HashSet<AbnormalityCodexEntryDef>();
            if (map?.listerBuildings == null)
            {
                return contained;
            }

            List<Building_AbnormalityContainmentTerminal> terminals =
                map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityContainmentTerminal>().ToList();
            for (int i = 0; i < terminals.Count; i++)
            {
                Building_AbnormalityContainmentTerminal terminal = terminals[i];
                if (terminal?.HeldPawn != null)
                {
                    AbnormalityCodexEntryDef codex = AbnormalityUtility.ResolveCodexEntry(terminal.HeldPawn.def);
                    if (codex != null)
                    {
                        contained.Add(codex);
                    }
                }
            }

            return contained;
        }

        private List<ThingDef> RollCandidatesForTier(LCExtractionTier tier, HashSet<ThingDef> excludeDefs)
        {
            HashSet<AbnormalityCodexEntryDef> contained = GetContainedCodexSet();
            HashSet<AbnormalityCodexEntryDef> pickedCodex = new HashSet<AbnormalityCodexEntryDef>();
            List<ThingDef> result = new List<ThingDef>(3);

            string[] slotPools = GetSlotRiskLevels(tier);
            for (int slot = 0; slot < 3; slot++)
            {
                string preferredLevel = slotPools[slot];
                ThingDef picked = PickSingleCandidate(preferredLevel, tier, contained, pickedCodex, excludeDefs, result);
                if (picked == null)
                {
                    // 兜底：从所有常规等级尝试
                    string[] fallbackLevels = { "ZAYIN", "TETH", "HE", "WAW", "ALEPH" };
                    for (int f = 0; f < fallbackLevels.Length && picked == null; f++)
                    {
                        picked = PickSingleCandidate(fallbackLevels[f], tier, contained, pickedCodex, excludeDefs, result);
                    }
                }

                if (picked != null)
                {
                    result.Add(picked);
                    AbnormalityCodexEntryDef codex = AbnormalityUtility.ResolveCodexEntry(picked);
                    if (codex != null)
                    {
                        pickedCodex.Add(codex);
                    }
                }
            }

            return result;
        }

        private static string[] GetSlotRiskLevels(LCExtractionTier tier)
        {
            switch (tier)
            {
                case LCExtractionTier.Shallow:
                    return new[]
                    {
                        Rand.Chance(0.60f) ? "ZAYIN" : "TETH",
                        Rand.Chance(0.55f) ? "ZAYIN" : "TETH",
                        Rand.Chance(0.50f) ? "TETH" : "ZAYIN"
                    };
                case LCExtractionTier.Mid:
                    return new[]
                    {
                        Rand.Chance(0.45f) ? "TETH" : "HE",
                        "HE",
                        Rand.Chance(0.35f) ? "TETH" : "HE"
                    };
                case LCExtractionTier.Deep:
                    return new[]
                    {
                        "HE",
                        Rand.Chance(0.55f) ? "HE" : "WAW",
                        "WAW"
                    };
                case LCExtractionTier.Abyss:
                    return new[]
                    {
                        "ALEPH",
                        Rand.Chance(0.65f) ? "WAW" : "ALEPH",
                        "WAW"
                    };
                case LCExtractionTier.Standard:
                default:
                    return new[]
                    {
                        Rand.Chance(0.65f) ? "ZAYIN" : "TETH",
                        Rand.Chance(0.60f) ? "TETH" : "HE",
                        Rand.Chance(0.55f) ? "HE" : "ZAYIN"
                    };
            }
        }

        private ThingDef PickSingleCandidate(
            string level,
            LCExtractionTier tier,
            HashSet<AbnormalityCodexEntryDef> contained,
            HashSet<AbnormalityCodexEntryDef> pickedCodex,
            HashSet<ThingDef> excludeDefs,
            List<ThingDef> alreadyPicked)
        {
            List<ThingDef_AbnormalityBase> rawPool = ExtractUtil.Get_AnomlayLvl2DefList_Ritual(level);
            if (rawPool == null || rawPool.Count == 0)
            {
                return null;
            }

            // 第一轮过滤：未在本轮出现、同图鉴未重复、未在 excludeDefs、尚未在场收容
            List<ThingDef> preferred = FilterPool(rawPool, contained, pickedCodex, excludeDefs, alreadyPicked, allowContained: false, allowExcluded: false, allowSameCodex: false);
            if (preferred.Count > 0)
            {
                return preferred.RandomElement();
            }

            // 第二轮放宽：允许本图已收容
            List<ThingDef> allowContainedPool = FilterPool(rawPool, contained, pickedCodex, excludeDefs, alreadyPicked, allowContained: true, allowExcluded: false, allowSameCodex: false);
            if (allowContainedPool.Count > 0)
            {
                return allowContainedPool.RandomElement();
            }

            // 第三轮放宽：允许重抽旧候选（针对 ALEPH 等仅有少量实体的池）
            List<ThingDef> allowExcludedPool = FilterPool(rawPool, contained, pickedCodex, excludeDefs, alreadyPicked, allowContained: true, allowExcluded: true, allowSameCodex: false);
            if (allowExcludedPool.Count > 0)
            {
                return allowExcludedPool.RandomElement();
            }

            // 若当前等级已全部被抽走（例如只有 1 个 ALEPH 且 slot 1/2 再随到 ALEPH），对于非首槽改为尝试 WAW
            if (level == "ALEPH" && alreadyPicked.Count > 0 && tier == LCExtractionTier.Abyss)
            {
                return PickSingleCandidate("WAW", tier, contained, pickedCodex, excludeDefs, alreadyPicked);
            }

            List<ThingDef> lastResort = FilterPool(rawPool, contained, pickedCodex, excludeDefs, alreadyPicked, allowContained: true, allowExcluded: true, allowSameCodex: true);
            return lastResort.Count > 0 ? lastResort.RandomElement() : null;
        }

        private static List<ThingDef> FilterPool(
            List<ThingDef_AbnormalityBase> pool,
            HashSet<AbnormalityCodexEntryDef> contained,
            HashSet<AbnormalityCodexEntryDef> pickedCodex,
            HashSet<ThingDef> excludeDefs,
            List<ThingDef> alreadyPicked,
            bool allowContained,
            bool allowExcluded,
            bool allowSameCodex)
        {
            List<ThingDef> filtered = new List<ThingDef>();
            for (int i = 0; i < pool.Count; i++)
            {
                ThingDef def = pool[i];
                if (def == null || alreadyPicked.Contains(def))
                {
                    continue;
                }

                if (!allowExcluded && excludeDefs != null && excludeDefs.Contains(def))
                {
                    continue;
                }

                AbnormalityCodexEntryDef codex = AbnormalityUtility.ResolveCodexEntry(def);
                if (!allowSameCodex && codex != null && pickedCodex.Contains(codex))
                {
                    continue;
                }

                if (!allowContained && codex != null && contained.Contains(codex))
                {
                    continue;
                }

                filtered.Add(def);
            }

            return filtered;
        }
    }
}
