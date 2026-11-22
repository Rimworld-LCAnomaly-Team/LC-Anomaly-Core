using LCAnomalyCore.Defs;
using LCAnomalyCore.ModExtensions;
using System;
using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 提取仪式相关操作的工具类
    /// </summary>
    public static class ExtractUtil
    {
        #region 字段

        private static bool hasInited = false;

        private static readonly List<Func<Def, bool>> EligibilityFilters = new List<Func<Def, bool>>();

        private static string[] sAnomalyLevel = ["ZAYIN", "TETH", "HE", "WAW", "ALEPH"];

        private static List<ThingDef_AbnormalityBase> anomalyDefList_Ritual_ZAYIN = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Ritual_TETH = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Ritual_HE = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Ritual_WAW = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Ritual_ALEPH = [];

        private static List<ThingDef_AbnormalityBase> anomalyDefList_Cogito_ZAYIN = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Cogito_TETH = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Cogito_HE = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Cogito_WAW = [];
        private static List<ThingDef_AbnormalityBase> anomalyDefList_Cogito_ALEPH = [];

        private static List<PawnKindDef_AbnormalityBase> anomalyDefList_SevenSin_ZAYIN = [];
        private static List<PawnKindDef_AbnormalityBase> anomalyDefList_SevenSin_TETH = [];
        private static List<PawnKindDef_AbnormalityBase> anomalyDefList_SevenSin_HE = [];
        private static List<PawnKindDef_AbnormalityBase> anomalyDefList_SevenSin_WAW = [];
        private static List<PawnKindDef_AbnormalityBase> anomalyDefList_SevenSin_ALEPH = [];

        /// <summary>
        /// 字典：异想体等级->所有指定等级异想体的列表（仅限仪式召唤）
        /// </summary>
        private static Dictionary<string, List<ThingDef_AbnormalityBase>> anomlayLvl2DefList_Ritual;

        /// <summary>
        /// 字典：异想体等级->所有指定等级异想体的列表（仅限Cogito注射）
        /// </summary>
        private static Dictionary<string, List<ThingDef_AbnormalityBase>> anomlayLvl2DefList_Cogito;

        /// <summary>
        /// 字典：异想体等级->所有指定等级异想体的列表（仅限大罪生物）
        /// </summary>
        private static Dictionary<string, List<PawnKindDef_AbnormalityBase>> anomlayLvl2DefList_SevenSin;

        #endregion 字段

        #region 初始化

        /// <summary>
        /// 初始化
        /// </summary>
        private static void Init()
        {
            anomlayLvl2DefList_Ritual = new Dictionary<string, List<ThingDef_AbnormalityBase>>
            {
                {sAnomalyLevel[0],anomalyDefList_Ritual_ZAYIN},
                {sAnomalyLevel[1],anomalyDefList_Ritual_TETH},
                {sAnomalyLevel[2],anomalyDefList_Ritual_HE},
                {sAnomalyLevel[3],anomalyDefList_Ritual_WAW},
                {sAnomalyLevel[4],anomalyDefList_Ritual_ALEPH}
            };

            anomlayLvl2DefList_Cogito = new Dictionary<string, List<ThingDef_AbnormalityBase>>
            {
                {sAnomalyLevel[0],anomalyDefList_Cogito_ZAYIN},
                {sAnomalyLevel[1],anomalyDefList_Cogito_TETH},
                {sAnomalyLevel[2],anomalyDefList_Cogito_HE},
                {sAnomalyLevel[3],anomalyDefList_Cogito_WAW},
                {sAnomalyLevel[4],anomalyDefList_Cogito_ALEPH}
            };

            anomlayLvl2DefList_SevenSin = new Dictionary<string, List<PawnKindDef_AbnormalityBase>>
            {
                {sAnomalyLevel[0],anomalyDefList_SevenSin_ZAYIN},
                {sAnomalyLevel[1],anomalyDefList_SevenSin_TETH},
                {sAnomalyLevel[2],anomalyDefList_SevenSin_HE},
                {sAnomalyLevel[3],anomalyDefList_SevenSin_WAW},
                {sAnomalyLevel[4],anomalyDefList_SevenSin_ALEPH}
            };

            foreach (var level in sAnomalyLevel)
            {
                //Ritual字典初始化
                foreach (var def in DefDatabase<ThingDef_AbnormalityEgg>.AllDefsListForReading)
                {
                    var defName = def.GetModExtension<ModExtension_AbnormalityCategory>()?.abnormalityCategoryDef?.defName;
                    if (defName == level)
                    {
                        anomlayLvl2DefList_Ritual[level].Add(def);
                    }
                }
                foreach (var def in DefDatabase<ThingDef_AbnormalityTool>.AllDefsListForReading)
                {
                    var defName = def.GetModExtension<ModExtension_AbnormalityCategory>()?.abnormalityCategoryDef?.defName;
                    if (defName == level)
                    {
                        anomlayLvl2DefList_Ritual[level].Add(def);
                    }
                }

                //Cogito字典初始化
                foreach (var def in DefDatabase<ThingDef_AbnormalityEntity_Spawn>.AllDefsListForReading)
                {
                    var defName = def.GetModExtension<ModExtension_AbnormalityCategory>()?.abnormalityCategoryDef?.defName;
                    if (defName == level)
                    {
                        anomlayLvl2DefList_Cogito[level].Add(def);
                    }
                }

                //SevenSin字典初始化
                foreach (var def in DefDatabase<PawnKindDef_AnomalyEntity_SevenSin>.AllDefsListForReading)
                {
                    var defName = def.GetModExtension<ModExtension_AbnormalityCategory>()?.abnormalityCategoryDef?.defName;
                    if (defName == level)
                    {
                        anomlayLvl2DefList_SevenSin[level].Add(def);
                    }
                }
            }

            hasInited = true;
        }

        /// <summary>
        /// 检查是否已经初始化过
        /// </summary>
        private static void CheckHasInitial()
        {
            if (hasInited) return;

            Init();
        }

        #endregion 初始化

        #region 工具方法

        /// <summary>注册提取池资格过滤器。任一过滤器返回 false 时该条目不会出现在抽取结果中。</summary>
        public static void RegisterEligibilityFilter(Func<Def, bool> filter)
        {
            if (filter == null || EligibilityFilters.Contains(filter))
            {
                return;
            }

            EligibilityFilters.Add(filter);
        }

        /// <summary>注销提取池资格过滤器。</summary>
        public static void UnregisterEligibilityFilter(Func<Def, bool> filter)
        {
            if (filter == null)
            {
                return;
            }

            EligibilityFilters.Remove(filter);
        }

        /// <summary>检查指定异想体 Def 当前是否允许被提取。</summary>
        public static bool IsEligibleForExtraction(Def def)
        {
            if (def == null)
            {
                return false;
            }

            for (int i = 0; i < EligibilityFilters.Count; i++)
            {
                Func<Def, bool> filter = EligibilityFilters[i];
                if (filter == null)
                {
                    continue;
                }

                try
                {
                    if (!filter(def))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce($"LC Anomaly Core: extraction eligibility filter failed for {def.defName}: {ex}", def.shortHash ^ 0x4C4317);
                }
            }

            return true;
        }

        /// <summary>
        /// 通过 异想体等级 获取 所有指定等级异想体的列表（仅限仪式召唤）
        /// </summary>
        /// <param name="level">异想体等级</param>
        /// <returns>指定等级异想体的列表（仅限仪式召唤）</returns>
        public static List<ThingDef_AbnormalityBase> Get_AnomlayLvl2DefList_Ritual(string level)
        {
            if (CheckIfLevelLegal(level))
            {
                CheckHasInitial();
                return FilterEligible(anomlayLvl2DefList_Ritual[level]);
            }

            return new List<ThingDef_AbnormalityBase>();
        }

        /// <summary>
        /// 通过 异想体等级 获取 所有指定等级异想体的列表（仅限Cogito注射）
        /// </summary>
        /// <param name="level">异想体等级</param>
        /// <returns>指定等级异想体的列表（仅限Cogito注射）</returns>
        public static List<ThingDef_AbnormalityBase> Get_AnomlayLvl2DefList_Cogito(string level)
        {
            if (CheckIfLevelLegal(level))
            {
                CheckHasInitial();
                return FilterEligible(anomlayLvl2DefList_Cogito[level]);
            }

            return new List<ThingDef_AbnormalityBase>();
        }

        /// <summary>
        /// 通过 异想体等级 获取 所有指定等级异想体的列表（仅限大罪生物）
        /// </summary>
        /// <param name="level">异想体等级</param>
        /// <returns>指定等级异想体的列表（仅限大罪生物）</returns>
        public static List<PawnKindDef_AbnormalityBase> Get_AnomlayLvl2DefList_SevenSin(string level)
        {
            if (CheckIfLevelLegal(level))
            {
                CheckHasInitial();
                return FilterEligible(anomlayLvl2DefList_SevenSin[level]);
            }

            return new List<PawnKindDef_AbnormalityBase>();
        }

        private static List<T> FilterEligible<T>(List<T> source) where T : Def
        {
            List<T> result = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (IsEligibleForExtraction(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// 检查等级是否合法
        /// </summary>
        /// <param name="s">等级字符串</param>
        /// <returns>是否合法</returns>
        private static bool CheckIfLevelLegal(string s)
        {
            foreach (var level in sAnomalyLevel)
            {
                if (s == level)
                    return true;
            }

            return false;
        }

        #endregion 工具方法
    }
}
