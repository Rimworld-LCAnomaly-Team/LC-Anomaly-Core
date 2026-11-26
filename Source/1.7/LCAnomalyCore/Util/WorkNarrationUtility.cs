using System.Collections.Generic;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Misc;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 收容工作期间浮现旁白文本的本地化解析与 Mote 生成工具。
    /// </summary>
    public static class WorkNarrationUtility
    {
        private static readonly List<string> TmpCandidates = new List<string>(8);

        /// <summary>按工作类型返回对应的脑叶色调高亮色。</summary>
        public static Color GetWorkTypeAccentColor(EAnomalyWorkType workType)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct: return new Color(0.94f, 0.34f, 0.30f);
                case EAnomalyWorkType.Insight: return new Color(0.36f, 0.78f, 0.98f);
                case EAnomalyWorkType.Attachment: return new Color(0.92f, 0.45f, 0.78f);
                case EAnomalyWorkType.Repression: return new Color(0.68f, 0.48f, 0.96f);
                default: return new Color(0.36f, 0.78f, 0.98f);
            }
        }

        /// <summary>按工作结算评级返回对应的状态提示色。</summary>
        public static Color GetResultAccentColor(LC_StudyResult result)
        {
            switch (result)
            {
                case LC_StudyResult.Good: return new Color(0.32f, 0.94f, 0.56f);
                case LC_StudyResult.Normal: return new Color(0.96f, 0.78f, 0.30f);
                case LC_StudyResult.Bad: return new Color(0.96f, 0.28f, 0.30f);
                default: return new Color(0.96f, 0.78f, 0.30f);
            }
        }

        /// <summary>解析工作开始时的本地化旁白文本（优先异想体专属词条，回退按工作类型通用词条）。</summary>
        public static string ResolveStartLine(Pawn studier, Pawn abnormality, EAnomalyWorkType workType, string workTypeLabel)
        {
            string workerName = GetWorkerName(studier);
            string abnoName = GetAbnormalityName(abnormality);
            string defName = abnormality?.def?.defName;
            string resolvedWorkLabel = workTypeLabel.NullOrEmpty() ? workType.ToString() : workTypeLabel;

            TmpCandidates.Clear();
            if (!defName.NullOrEmpty())
            {
                for (int i = 1; i <= 2; i++)
                {
                    string key = $"LC_WorkNarration_{defName}_Start_{i}";
                    if (key.CanTranslate())
                    {
                        TmpCandidates.Add(key);
                    }
                }
            }

            if (TmpCandidates.Count == 0)
            {
                for (int i = 1; i <= 2; i++)
                {
                    string key = $"LC_WorkNarration_Generic_Start_{workType}_{i}";
                    if (key.CanTranslate())
                    {
                        TmpCandidates.Add(key);
                    }
                }
            }

            if (TmpCandidates.Count == 0)
            {
                return null;
            }

            string pickedKey = TmpCandidates.RandomElement();
            TmpCandidates.Clear();
            return pickedKey.Translate(workerName, abnoName, resolvedWorkLabel).ToString();
        }

        /// <summary>解析工作中段巡检时的本地化旁白文本（按会话序号轮换避免连句重复）。</summary>
        public static string ResolveMidLine(Pawn studier, Pawn abnormality, EAnomalyWorkType workType, string workTypeLabel, int sequenceIndex)
        {
            string workerName = GetWorkerName(studier);
            string abnoName = GetAbnormalityName(abnormality);
            string defName = abnormality?.def?.defName;
            string resolvedWorkLabel = workTypeLabel.NullOrEmpty() ? workType.ToString() : workTypeLabel;

            TmpCandidates.Clear();
            if (!defName.NullOrEmpty())
            {
                for (int i = 1; i <= 6; i++)
                {
                    string key = $"LC_WorkNarration_{defName}_Mid_{i}";
                    if (key.CanTranslate())
                    {
                        TmpCandidates.Add(key);
                    }
                }
            }

            if (TmpCandidates.Count == 0)
            {
                for (int i = 1; i <= 4; i++)
                {
                    string key = $"LC_WorkNarration_Generic_Mid_{workType}_{i}";
                    if (key.CanTranslate())
                    {
                        TmpCandidates.Add(key);
                    }
                }
            }

            if (TmpCandidates.Count == 0)
            {
                return null;
            }

            int idx = Mathf.Abs(sequenceIndex) % TmpCandidates.Count;
            string pickedKey = TmpCandidates[idx];
            TmpCandidates.Clear();
            return pickedKey.Translate(workerName, abnoName, resolvedWorkLabel).ToString();
        }

        /// <summary>解析工作结算时的本地化旁白文本。</summary>
        public static string ResolveResultLine(Pawn studier, Pawn abnormality, LC_StudyResult result)
        {
            string workerName = GetWorkerName(studier);
            string abnoName = GetAbnormalityName(abnormality);
            string defName = abnormality?.def?.defName;

            if (!defName.NullOrEmpty())
            {
                string specificKey = $"LC_WorkNarration_{defName}_Result_{result}";
                if (specificKey.CanTranslate())
                {
                    return specificKey.Translate(workerName, abnoName).ToString();
                }
            }

            string genericKey = $"LC_WorkNarration_Generic_Result_{result}";
            if (genericKey.CanTranslate())
            {
                return genericKey.Translate(workerName, abnoName).ToString();
            }

            return null;
        }

        /// <summary>在地图指定世界坐标生成收容工作旁白飘字 Mote。</summary>
        public static Mote_LCWorkNarration SpawnNarration(Map map, Vector3 worldPos, string text, Color accentColor)
        {
            if (map == null || text.NullOrEmpty() || Defs.ThingDefOf.LC_Mote_WorkNarration == null)
            {
                return null;
            }

            IntVec3 cell = worldPos.ToIntVec3();
            if (!cell.InBounds(map))
            {
                return null;
            }

            Mote_LCWorkNarration mote = (Mote_LCWorkNarration)ThingMaker.MakeThing(Defs.ThingDefOf.LC_Mote_WorkNarration);
            mote.exactPosition = worldPos;
            mote.SetVelocity(Rand.Range(-8f, 8f), 0.24f);
            mote.narrationText = text;
            mote.accentColor = accentColor;
            GenSpawn.Spawn(mote, cell, map);
            return mote;
        }

        private static string GetWorkerName(Pawn studier)
        {
            return studier != null ? studier.LabelShortCap : "LC_WorkNarration_FallbackWorker".Translate().ToString();
        }

        private static string GetAbnormalityName(Pawn abnormality)
        {
            return abnormality != null ? abnormality.LabelShortCap : "LC_WorkNarration_FallbackAbnormality".Translate().ToString();
        }
    }
}
