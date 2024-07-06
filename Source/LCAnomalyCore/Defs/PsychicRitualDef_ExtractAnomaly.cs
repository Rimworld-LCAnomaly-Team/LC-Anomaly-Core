using LCAnomalyLibrary.Defs;
using LCAnomalyLibrary.PsychicRituals;
using LCAnomalyLibrary.Util;
using System.Collections.Generic;
using Verse.AI.Group;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 提取异想体仪式
    /// </summary>
    public class PsychicRitualDef_ExtractAnomaly : ExtractRitualDef_InvocationCircle
    {
        private string anomalyLevelTag;

        /// <summary>
        /// 创建Toils
        /// </summary>
        /// <param name="psychicRitual">仪式</param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public override List<PsychicRitualToil> CreateToils(PsychicRitual psychicRitual, PsychicRitualGraph parent)
        {
            List<PsychicRitualToil> list = base.CreateToils(psychicRitual, parent);
            list.Add(new PsychicRitualToil_ExtractAnomaly(InvokerRole, TargetRole, anomalyLevelTag));
            return list;
        }

        /// <summary>
        /// 提示信息
        /// </summary>
        /// <param name="pawn">对象</param>
        /// <returns></returns>
        public override IEnumerable<string> GetPawnTooltipExtras(Pawn pawn)
        {
            if (pawn.guest != null && !pawn.guest.Recruitable)
            {
                yield return "NonRecruitable".Translate();
            }
        }

        /// <summary>
        /// 检查仪式是否满足条件
        /// </summary>
        /// <param name="psychicRitual">仪式</param>
        public override void CheckPsychicRitualCancelConditions(PsychicRitual psychicRitual)
        {
            base.CheckPsychicRitualCancelConditions(psychicRitual);

            //验证Tag是否正确，Tag对应异想体是否存在，若不满足任意条件则终止仪式
            var list = ExtractUtil.Get_AnomlayLvl2DefList_Ritual(anomalyLevelTag);
            if (list == null)
            {
                psychicRitual.CancelPsychicRitual("PsychicRitualDef_InvocationCircle_InvokerLost".Translate());
                Log.Warning("提取仪式因Tag错误而失败");
            }
            if (list.Count <= 0)
            {
                psychicRitual.CancelPsychicRitual("PsychicRitualDef_InvocationCircle_InvokerLost".Translate());
                Log.Warning("提取仪式因列表为空失败");
            }
        }
    }
}
