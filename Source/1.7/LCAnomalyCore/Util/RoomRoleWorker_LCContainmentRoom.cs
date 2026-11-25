using System.Collections.Generic;
using LCAnomalyCore.Buildings;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// 识别包含收容室控制终端的房间为“异想体收容室”。
    /// </summary>
    public class RoomRoleWorker_LCContainmentRoom : RoomRoleWorker
    {
        /// <inheritdoc />
        public override float GetScore(Room room)
        {
            if (room == null || room.TouchesMapEdge)
            {
                return 0f;
            }

            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building_AbnormalityContainmentTerminal)
                {
                    return 100000f;
                }
            }

            return 0f;
        }
    }
}
