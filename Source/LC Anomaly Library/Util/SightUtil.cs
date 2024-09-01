using RimWorld;
using System;
using System.Linq;
using Verse;

namespace LCAnomalyLibrary.Util
{
    public static class SightUtil
    {
        /// <summary>
        /// 判断目标是否在视野里
        /// </summary>
        /// <param name="thing">目标</param>
        /// <param name="faction">派系</param>
        /// <returns></returns>
        public static bool IsOutsideView(Thing thing, Faction faction)
        {
            if (thing.SpawnedOrAnyParentSpawned && thing.MapHeld.reservationManager.IsReservedByAnyoneOf(thing, faction))
            {
                return false;
            }

            if (Find.CurrentMap != thing.MapHeld)
            {
                return true;
            }

            return !Find.CameraDriver.CurrentViewRect.ExpandedBy(1).Contains(thing.PositionHeld);
        }
    }
}
