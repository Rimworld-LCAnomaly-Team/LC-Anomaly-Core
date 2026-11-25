using System.Collections.Generic;
using Verse;

namespace LCAnomalyCore.Buildings
{
    /// <summary>放置校验：若所在室内房间已存在另一台收容控制终端，则禁止重复放置。</summary>
    public class PlaceWorker_AbnormalityContainmentTerminal : PlaceWorker
    {
        /// <inheritdoc />
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (map == null || !loc.InBounds(map))
            {
                return true;
            }

            Room room = loc.GetRoom(map);
            if (room == null || room.TouchesMapEdge || room.PsychologicallyOutdoors)
            {
                return true;
            }

            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing item = things[i];
                if (item == null || item == thingToIgnore)
                {
                    continue;
                }

                if (item is Building_AbnormalityContainmentTerminal)
                {
                    return "LC_ContainmentRoom_DuplicateTerminal".Translate();
                }

                if (item.def?.entityDefToBuild != null && item.def.entityDefToBuild.defName == "LC_ContainmentTerminal")
                {
                    return "LC_ContainmentRoom_DuplicateTerminal".Translate();
                }
            }

            return true;
        }
    }
}
