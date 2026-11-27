using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LCAnomalyCore.Comp;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>图鉴条目的归档大类。</summary>
    public enum CodexEntryKind
    {
        Auto = 0,
        Abnormality = 1,
        Tool = 2,
        Ordeal = 3
    }

    /// <summary>脑叶图鉴可渐进解锁的情报模块位掩码。</summary>
    [Flags]
    public enum CodexIntelSection
    {
        None = 0,
        BasicInfo = 1,
        WorkPreferences = 2,
        ManagementTips = 4,
        EscapeCombat = 8,
        All = BasicInfo | WorkPreferences | ManagementTips | EscapeCombat
    }

    /// <summary>表示 <c>AbnormalityCodexEntryDef</c> 类型。</summary>
    public class AbnormalityCodexEntryDef : Def
    {
        private static readonly Regex ContainmentCodeRegex = new Regex(@"\b[OTFAD0]-\d{2}-\d{2}\b", RegexOptions.Compiled);

        /// <summary>
        /// 别名列表（不同观察等级的不同名字 / 收容编号）
        /// </summary>
        public List<string> aliasLabels;

        /// <summary>
        /// 分类等级
        /// </summary>
        public AbnormalityCategoryDef category;

        /// <summary>表示 <c>orderInCategory</c>。</summary>
        public int orderInCategory = 9999999;

        /// <summary>图鉴条目所属归档大类（Auto 时自动依据实体组件推断）。</summary>
        public CodexEntryKind entryKind = CodexEntryKind.Auto;

        /// <summary>收容编号（例如 O-03-03、ORDEAL-D1）；留空时自动从 aliasLabels 或描述提取。</summary>
        [NoTranslate]
        public string containmentCode;

        /// <summary>考验所属阶段标识（例如 Dawn、Noon、Dusk、Midnight、White）。</summary>
        [NoTranslate]
        public string ordealStage;

        /// <summary>单模块情报解锁所需的独立 PE-Box 数量（小于等于 0 时按危险等级自动计算）。</summary>
        public int intelCostPerSection = 0;

        [NoTranslate]
        private string uiIconPath = null;

        /// <summary>表示 <c>icon</c>。</summary>
        public Texture2D icon;

        /// <summary>表示 <c>silhouette</c>。</summary>
        public Texture2D silhouette;

        /// <summary>表示 <c>Visible</c>。</summary>
        public bool Visible = true;

        /// <summary>表示 <c>Discovered</c>。</summary>
        public bool Discovered
        {
            get => Util.Components.LC?.IsAbnormalityDiscovered(this) ?? false;
            set => Util.Components.LC?.SetAbnormalityDiscovered(this, value);
        }

        /// <summary>与图鉴条目关联的实体或物品 Def。</summary>
        public List<ThingDef> linkedThings = new List<ThingDef>();

        /// <summary>与图鉴条目关联的解锁研究项目。</summary>
        public List<ResearchProjectDef> discoveredResearchProjects = new List<ResearchProjectDef>();

        private const string SilhouetteTexPathSuffix = "_Silhouette";

        private static bool HasAbnormalityOrToolComp(ThingDef td)
        {
            if (td == null)
            {
                return false;
            }

            if (td is ThingDef_AbnormalityTool)
            {
                return true;
            }

            if (td.comps != null)
            {
                for (int i = 0; i < td.comps.Count; i++)
                {
                    CompProperties cp = td.comps[i];
                    if (cp is CompProperties_Abnormality || cp is CompProperties_AbnormalityTool)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasToolComp(ThingDef td)
        {
            if (td == null)
            {
                return false;
            }

            if (td is ThingDef_AbnormalityTool)
            {
                return true;
            }

            if (td.comps != null)
            {
                for (int i = 0; i < td.comps.Count; i++)
                {
                    if (td.comps[i] is CompProperties_AbnormalityTool)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>解析该条目对应的核心实体 ThingDef（优先拥有收容/工具组件或非蛋种族定义）。</summary>
        public ThingDef PrimaryEntityDef
        {
            get
            {
                if (linkedThings != null)
                {
                    for (int i = 0; i < linkedThings.Count; i++)
                    {
                        ThingDef td = linkedThings[i];
                        if (HasAbnormalityOrToolComp(td))
                        {
                            return td;
                        }
                    }

                    for (int i = 0; i < linkedThings.Count; i++)
                    {
                        ThingDef td = linkedThings[i];
                        if (td != null && td.race != null && !td.defName.EndsWith("Egg", StringComparison.Ordinal))
                        {
                            return td;
                        }
                    }

                    for (int i = 0; i < linkedThings.Count; i++)
                    {
                        if (linkedThings[i] != null)
                        {
                            return linkedThings[i];
                        }
                    }
                }

                return DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            }
        }

        /// <summary>解析最终条目分类（异想体 / 工具型 / 考验）。</summary>
        public CodexEntryKind ResolvedEntryKind
        {
            get
            {
                if (entryKind != CodexEntryKind.Auto)
                {
                    return entryKind;
                }

                if (linkedThings != null)
                {
                    for (int i = 0; i < linkedThings.Count; i++)
                    {
                        if (HasToolComp(linkedThings[i]))
                        {
                            return CodexEntryKind.Tool;
                        }
                    }
                }

                return CodexEntryKind.Abnormality;
            }
        }

        /// <summary>解析显示用的脑叶收容编号。</summary>
        public string ResolvedContainmentCode
        {
            get
            {
                if (!containmentCode.NullOrEmpty())
                {
                    return containmentCode;
                }

                if (!aliasLabels.NullOrEmpty())
                {
                    for (int i = 0; i < aliasLabels.Count; i++)
                    {
                        string alias = aliasLabels[i];
                        if (!alias.NullOrEmpty() && ContainmentCodeRegex.IsMatch(alias))
                        {
                            return alias.Trim();
                        }
                    }

                    for (int i = 0; i < aliasLabels.Count; i++)
                    {
                        if (!aliasLabels[i].NullOrEmpty())
                        {
                            return aliasLabels[i].Trim();
                        }
                    }
                }

                if (!description.NullOrEmpty())
                {
                    Match m = ContainmentCodeRegex.Match(description);
                    if (m.Success)
                    {
                        return m.Value;
                    }
                }

                return defName;
            }
        }

        /// <summary>该条目实际适用并计入图鉴完成度的情报模块集合。</summary>
        public CodexIntelSection ApplicableIntelSections
        {
            get
            {
                switch (ResolvedEntryKind)
                {
                    case CodexEntryKind.Tool:
                        return CodexIntelSection.BasicInfo | CodexIntelSection.ManagementTips;
                    case CodexEntryKind.Ordeal:
                        return CodexIntelSection.BasicInfo | CodexIntelSection.EscapeCombat;
                    default:
                        return CodexIntelSection.All;
                }
            }
        }

        /// <summary>单模块情报解锁需消耗的独立 PE-Box 数量。</summary>
        public int IntelUnlockCost
        {
            get
            {
                if (intelCostPerSection > 0)
                {
                    return intelCostPerSection;
                }

                switch (category?.defName)
                {
                    case "ZAYIN": return 4;
                    case "TETH": return 6;
                    case "HE": return 10;
                    case "WAW": return 16;
                    case "ALEPH": return 24;
                    default: return 8;
                }
            }
        }

        /// <inheritdoc />
        public override void PostLoad()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                icon = uiIconPath.NullOrEmpty()
                    ? BaseContent.BadTex
                    : ContentFinder<Texture2D>.Get(uiIconPath, reportFailure: false) ?? BaseContent.BadTex;
                Texture2D rawSilhouette = uiIconPath.NullOrEmpty()
                    ? null
                    : ContentFinder<Texture2D>.Get(uiIconPath + SilhouetteTexPathSuffix, reportFailure: false);
                silhouette = rawSilhouette ?? icon;
            });
        }

        /// <inheritdoc />
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string item in base.ConfigErrors())
            {
                yield return item;
            }

            if (category == null)
            {
                yield return "category is null.";
            }

            if (uiIconPath.NullOrEmpty())
            {
                yield return "missing icon.";
            }
        }
    }
}
