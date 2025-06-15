using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LCAnomalyCore.Util
{
    public static class HealthUtil
    {
        /// <summary>
        /// 获取pawn所有缺失的肢体
        /// </summary>
        /// <param name="pawn"></param>
        /// <returns></returns>
        public static List<BodyPartRecord> FindAllMissingBodyPart(Pawn pawn)
        {
            List<BodyPartRecord> bodyPartRecords = [];
            bodyPartRecords.AddRange(from Hediff_MissingPart missingPartsCommonAncestor in pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                                     where !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(missingPartsCommonAncestor.Part)
                                     select missingPartsCommonAncestor.Part);
            return bodyPartRecords;
        }
    }
}