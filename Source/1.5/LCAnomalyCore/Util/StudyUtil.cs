using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using System;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 研究工作工具类
    /// </summary>
    public static class StudyUtil
    {
        /// <summary>
        /// 播放工作质量特效
        /// </summary>
        /// <param name="studier">研究者</param>
        /// <param name="entity">被研究者</param>
        /// <param name="result">研究质量</param>
        public static void DoStudyResultEffect(Pawn studier, Pawn entity, LC_StudyResult result)
        {
            switch (result)
            {
                case LC_StudyResult.Good:
                    FleckMaker.Static(entity.PositionHeld, entity.MapHeld, Defs.FleckDefOf.WorkResult_Good);
                    //Log.Message($"工作：{studier.Name} 对异想体 {entity.def.label.Translate()} 工作成功，质量：良好。");
                    break;

                case LC_StudyResult.Normal:
                    FleckMaker.Static(entity.PositionHeld, entity.MapHeld, Defs.FleckDefOf.WorkResult_Normal);
                    //Log.Message($"工作：{studier.Name} 对异想体 {entity.def.label.Translate()} 工作成功，质量：普通。");
                    break;

                case LC_StudyResult.Bad:
                    FleckMaker.Static(entity.PositionHeld, entity.MapHeld, Defs.FleckDefOf.WorkResult_Bad);
                    //Log.Message($"工作：{studier.Name} 对异想体 {entity.def.label.Translate()} 工作失败，质量：差。");
                    break;

                default:
                    //Log.Error("工作：检测到错误的输入。");
                    break;
            }
        }

        /// <summary>
        /// 根据数值计算员工属性等级
        /// </summary>
        /// <returns></returns>
        public static EPawnLevel CalculatePawnLevel(float points)
        {
            if (points > 100)
                return EPawnLevel.EX;
            else if (points >= 85)
                return EPawnLevel.V;
            else if (points >= 65)
                return EPawnLevel.IV;
            else if (points >= 45)
                return EPawnLevel.III;
            else if (points >= 30)
                return EPawnLevel.II;
            else
                return EPawnLevel.I;
        }

        /// <summary>
        /// 计算处理脑叶员工数值变化量
        /// </summary>
        public static float GetPawnStatusIncreaseValue(CompPawnStatus studier, EAnomalyWorkType workType, string abnormalLevel)
        {
            EPawnLevel pawnLevel;
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Fortitude);
                    break;

                case EAnomalyWorkType.Attachment:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Temperance);
                    break;

                case EAnomalyWorkType.Insight:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Prudence);
                    break;

                case EAnomalyWorkType.Repression:
                    pawnLevel = studier.GetPawnStatusELevel(EPawnStatus.Justice);
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Unknown EPawnStatus");
            }

            return get_value(pawnLevel, abnormalLevel);

            //根据员工技能评级和异常等级来获取增加值
            static float get_value(EPawnLevel pawnLevel, string abnormalLevel)
            {
                //算法表格详见 https://lobotomycorp.fandom.com/zh/wiki/%E8%81%8C%E5%91%98
                if (abnormalLevel == "ZAYIN")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.44f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.3f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.18f;
                    else
                        return 0.08f;
                }
                else if (abnormalLevel == "TETH")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.55f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.4f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.27f;
                    else
                        return 0.16f;
                }
                else if (abnormalLevel == "HE")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.72f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.55f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.5f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.36f;
                    else
                        return 0.24f;
                }
                else if (abnormalLevel == "WAW")
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.84f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.66f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.5f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.45f;
                    else
                        return 0.32f;
                }
                else
                {
                    if (pawnLevel == EPawnLevel.I)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.II)
                        return 0.77f;
                    else if (pawnLevel == EPawnLevel.III)
                        return 0.6f;
                    else if (pawnLevel == EPawnLevel.IV)
                        return 0.45f;
                    else
                        return 0.4f;
                }
            }
        }
    }

    /// <summary>
    /// 研究质量枚举
    /// </summary>
    public enum LC_StudyResult
    {
        /// <summary>
        /// 良好
        /// </summary>
        Good,

        /// <summary>
        /// 一般
        /// </summary>
        Normal,

        /// <summary>
        /// 差
        /// </summary>
        Bad
    }
}