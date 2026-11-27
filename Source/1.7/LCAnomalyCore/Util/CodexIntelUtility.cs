using System;
using System.Collections.Generic;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Defs;
using LCAnomalyCore.GameComponent;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>图鉴整体完成度与里程碑档位快照。</summary>
    public struct CodexCompletionSnapshot
    {
        public int totalEntries;
        public int discoveredEntries;
        public int completedEntries;
        public int unlockedSlots;
        public int totalSlots;
        public float completionPercent;
        public int milestoneTier;
    }

    /// <summary>
    /// 独立管理图鉴的情报模块解锁、工作偏好矩阵查询、PE-Box 联动与档案里程碑增益计算工具。
    /// </summary>
    public static class CodexIntelUtility
    {
        public static readonly CodexIntelSection[] OrderedSections =
        {
            CodexIntelSection.BasicInfo,
            CodexIntelSection.WorkPreferences,
            CodexIntelSection.ManagementTips,
            CodexIntelSection.EscapeCombat
        };

        public static readonly EAnomalyWorkType[] OrderedWorkTypes =
        {
            EAnomalyWorkType.Instinct,
            EAnomalyWorkType.Insight,
            EAnomalyWorkType.Attachment,
            EAnomalyWorkType.Repression
        };

        /// <summary>统计位掩码中已激活的标准情报模块数量。</summary>
        public static int CountSections(CodexIntelSection mask)
        {
            int count = 0;
            for (int i = 0; i < OrderedSections.Length; i++)
            {
                if ((mask & OrderedSections[i]) == OrderedSections[i])
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>根据研究/观察进度与考验击杀记录计算应自动解锁的情报模块。</summary>
        public static CodexIntelSection ComputeAutoUnlockedIntel(AbnormalityCodexEntryDef entry, GameComponent_LC lc)
        {
            if (entry == null || lc == null || !lc.IsAbnormalityDiscovered(entry))
            {
                return CodexIntelSection.None;
            }

            CodexEntryKind kind = entry.ResolvedEntryKind;
            CodexIntelSection auto = CodexIntelSection.None;

            if (kind == CodexEntryKind.Ordeal)
            {
                auto |= CodexIntelSection.BasicInfo;
                if (lc.GetOrdealKillCount(entry) >= 1)
                {
                    auto |= CodexIntelSection.EscapeCombat;
                }

                return auto & entry.ApplicableIntelSections;
            }

            int studyProgress = GetMaxStudyProgress(entry, lc);
            int totalNotes = GetTotalStudyNotesCount(entry);

            if (kind == CodexEntryKind.Tool)
            {
                if (studyProgress >= 1)
                {
                    auto |= CodexIntelSection.BasicInfo;
                }

                if (studyProgress >= 2 || (totalNotes > 0 && studyProgress >= totalNotes))
                {
                    auto |= CodexIntelSection.BasicInfo | CodexIntelSection.ManagementTips;
                }

                return auto & entry.ApplicableIntelSections;
            }

            if (studyProgress >= 1)
            {
                auto |= CodexIntelSection.BasicInfo;
            }

            if (studyProgress >= 2)
            {
                auto |= CodexIntelSection.WorkPreferences;
            }

            if (studyProgress >= 3)
            {
                auto |= CodexIntelSection.ManagementTips;
            }

            if (studyProgress >= 4 || (totalNotes > 0 && totalNotes < 4 && studyProgress >= totalNotes))
            {
                if (totalNotes >= 3 || studyProgress >= 4)
                {
                    auto |= CodexIntelSection.EscapeCombat;
                }
            }

            return auto & entry.ApplicableIntelSections;
        }

        /// <summary>计算当前档案库全条目可见进度与 25%/50%/75%/100% 里程碑档位。</summary>
        public static CodexCompletionSnapshot GetCompletionSnapshot()
        {
            var snap = new CodexCompletionSnapshot();
            GameComponent_LC lc = GameComponent_LC.Instance;
            List<AbnormalityCodexEntryDef> all = DefDatabase<AbnormalityCodexEntryDef>.AllDefsListForReading;
            if (all == null || all.Count == 0)
            {
                return snap;
            }

            for (int i = 0; i < all.Count; i++)
            {
                AbnormalityCodexEntryDef entry = all[i];
                if (entry == null || !entry.Visible)
                {
                    continue;
                }

                snap.totalEntries++;
                CodexIntelSection applicable = entry.ApplicableIntelSections;
                int entrySlots = CountSections(applicable);
                snap.totalSlots += entrySlots;

                if (lc == null)
                {
                    continue;
                }

                if (lc.IsAbnormalityDiscovered(entry))
                {
                    snap.discoveredEntries++;
                }

                CodexIntelSection unlocked = lc.GetUnlockedCodexIntel(entry) & applicable;
                int unlockedCount = CountSections(unlocked);
                snap.unlockedSlots += unlockedCount;
                if (entrySlots > 0 && unlockedCount >= entrySlots)
                {
                    snap.completedEntries++;
                }
            }

            snap.completionPercent = snap.totalSlots > 0
                ? Mathf.Clamp01((float)snap.unlockedSlots / snap.totalSlots)
                : 0f;

            if (snap.totalSlots > 0 && snap.unlockedSlots >= snap.totalSlots)
            {
                snap.milestoneTier = 4;
            }
            else if (snap.completionPercent >= 0.75f)
            {
                snap.milestoneTier = 3;
            }
            else if (snap.completionPercent >= 0.50f)
            {
                snap.milestoneTier = 2;
            }
            else if (snap.completionPercent >= 0.25f)
            {
                snap.milestoneTier = 1;
            }

            return snap;
        }

        /// <summary>里程碑奖励：主管战术子弹容量加成（25% +1，100% 再 +1）。</summary>
        public static int GetBonusMaxManagerBullets()
        {
            int tier = GetCompletionSnapshot().milestoneTier;
            if (tier >= 4) return 2;
            if (tier >= 1) return 1;
            return 0;
        }

        /// <summary>里程碑奖励：收容工作额外 PE-Box 产出概率加成（50% +10%）。</summary>
        public static float GetBonusPeBoxChance()
        {
            return GetCompletionSnapshot().milestoneTier >= 2 ? 0.10f : 0f;
        }

        /// <summary>里程碑奖励：主管战术子弹充能冷却倍率（75% 缩短 15%）。</summary>
        public static float GetManagerBulletRechargeFactor()
        {
            return GetCompletionSnapshot().milestoneTier >= 3 ? 0.85f : 1f;
        }

        /// <summary>里程碑奖励：全图鉴 100% 解锁后免费获得每轮异想体三选一重抽机会。</summary>
        public static bool GrantsFreeAbnormalityReExtract()
        {
            return GetCompletionSnapshot().milestoneTier >= 4;
        }

        /// <summary>读取指定异想体在 I~V 级员工属性下的工作偏好矩阵。</summary>
        public static bool TryGetWorkMatrix(ThingDef entityDef, out Dictionary<EAnomalyWorkType, float[]> matrix)
        {
            matrix = null;
            CompProperties_Abnormality props = entityDef?.GetCompProperties<CompProperties_Abnormality>();
            if (props == null)
            {
                return false;
            }

            var result = new Dictionary<EAnomalyWorkType, float[]>();
            bool anyFound = false;
            for (int i = 0; i < OrderedWorkTypes.Length; i++)
            {
                EAnomalyWorkType workType = OrderedWorkTypes[i];
                if (props.TryGetWorkPreferenceRates(workType, out float[] rates) && rates != null && rates.Length > 0)
                {
                    result[workType] = NormalizeRates(rates);
                    anyFound = true;
                }
            }

            if (!anyFound)
            {
                return false;
            }

            matrix = result;
            return true;
        }

        private static float[] NormalizeRates(float[] raw)
        {
            float[] normalized = new float[5];
            float fallback = raw[raw.Length - 1];
            for (int i = 0; i < 5; i++)
            {
                float val = i < raw.Length ? raw[i] : fallback;
                normalized[i] = Mathf.Clamp01(val);
            }

            return normalized;
        }

        /// <summary>获取条目关联实体中保存的最大观察/研究等级。</summary>
        public static int GetMaxStudyProgress(AbnormalityCodexEntryDef entry, GameComponent_LC lc)
        {
            if (entry == null || lc == null)
            {
                return 0;
            }

            int max = 0;
            if (entry.linkedThings != null)
            {
                for (int i = 0; i < entry.linkedThings.Count; i++)
                {
                    ThingDef td = entry.linkedThings[i];
                    if (td != null && lc.AnomalyStatusSavedDict.TryGetValue(td, out AnomalyStatusSaved saved))
                    {
                        if (saved.StudyProgress > max)
                        {
                            max = saved.StudyProgress;
                        }
                    }
                }
            }

            ThingDef primary = entry.PrimaryEntityDef;
            if (primary != null && lc.AnomalyStatusSavedDict.TryGetValue(primary, out AnomalyStatusSaved primarySaved))
            {
                if (primarySaved.StudyProgress > max)
                {
                    max = primarySaved.StudyProgress;
                }
            }

            return max;
        }

        /// <summary>获取条目对应的总研究笔记数量。</summary>
        public static int GetTotalStudyNotesCount(AbnormalityCodexEntryDef entry)
        {
            ThingDef primary = entry?.PrimaryEntityDef;
            return primary?.GetCompProperties<CompProperties_AbnormalityStudyUnlocks>()?.studyNotes?.Count ?? 0;
        }

        /// <summary>统计条目及其关联定义下累计的独立 PE-Box 余量。</summary>
        public static int GetTotalIndiPeBox(AbnormalityCodexEntryDef entry, GameComponent_LC lc)
        {
            if (entry == null || lc == null)
            {
                return 0;
            }

            int total = 0;
            var seen = new HashSet<ThingDef>();
            if (entry.linkedThings != null)
            {
                for (int i = 0; i < entry.linkedThings.Count; i++)
                {
                    ThingDef td = entry.linkedThings[i];
                    if (td != null && seen.Add(td) && lc.AnomalyStatusSavedDict.TryGetValue(td, out AnomalyStatusSaved saved))
                    {
                        total += Mathf.Max(0, saved.IndiPeBoxAmount);
                    }
                }
            }

            ThingDef primary = entry.PrimaryEntityDef;
            if (primary != null && seen.Add(primary) && lc.AnomalyStatusSavedDict.TryGetValue(primary, out AnomalyStatusSaved primarySaved))
            {
                total += Mathf.Max(0, primarySaved.IndiPeBoxAmount);
            }

            return total;
        }

        /// <summary>返回当前持有独立 PE-Box 最多的关联 ThingDef（默认回退为主实体）。</summary>
        public static ThingDef GetIndiPeBoxHolderKey(AbnormalityCodexEntryDef entry, GameComponent_LC lc)
        {
            if (entry == null)
            {
                return null;
            }

            ThingDef bestDef = entry.PrimaryEntityDef;
            int bestAmount = -1;

            if (lc != null && entry.linkedThings != null)
            {
                for (int i = 0; i < entry.linkedThings.Count; i++)
                {
                    ThingDef td = entry.linkedThings[i];
                    if (td == null)
                    {
                        continue;
                    }

                    if (lc.AnomalyStatusSavedDict.TryGetValue(td, out AnomalyStatusSaved saved) && saved.IndiPeBoxAmount > bestAmount)
                    {
                        bestAmount = saved.IndiPeBoxAmount;
                        bestDef = td;
                    }
                }
            }

            return bestDef;
        }

        /// <summary>从条目关联的独立 PE-Box 存储池中扣除指定数量。</summary>
        public static bool TryConsumeIndiPeBox(AbnormalityCodexEntryDef entry, GameComponent_LC lc, int cost)
        {
            if (cost <= 0)
            {
                return true;
            }

            if (entry == null || lc == null || GetTotalIndiPeBox(entry, lc) < cost)
            {
                return false;
            }

            var candidates = new List<ThingDef>();
            ThingDef preferred = GetIndiPeBoxHolderKey(entry, lc);
            if (preferred != null)
            {
                candidates.Add(preferred);
            }

            if (entry.linkedThings != null)
            {
                for (int i = 0; i < entry.linkedThings.Count; i++)
                {
                    ThingDef td = entry.linkedThings[i];
                    if (td != null && !candidates.Contains(td))
                    {
                        candidates.Add(td);
                    }
                }
            }

            int remaining = cost;
            for (int i = 0; i < candidates.Count && remaining > 0; i++)
            {
                ThingDef td = candidates[i];
                if (!lc.AnomalyStatusSavedDict.TryGetValue(td, out AnomalyStatusSaved saved) || saved.IndiPeBoxAmount <= 0)
                {
                    continue;
                }

                int take = Mathf.Min(saved.IndiPeBoxAmount, remaining);
                saved.IndiPeBoxAmount -= take;
                lc.AnomalyStatusSavedDict[td] = saved;
                remaining -= take;
            }

            return remaining == 0;
        }
    }
}
