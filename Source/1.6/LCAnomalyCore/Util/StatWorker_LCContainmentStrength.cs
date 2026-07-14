using LCAnomalyCore.Comp;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    /// <summary>
    /// DLC-independent containment strength for LC holding platforms.
    /// </summary>
    public class StatWorker_LCContainmentStrength : StatWorker
    {
        private static readonly SimpleCurve WallStrengthFromHp = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(1000f, 100f),
            new CurvePoint(10000f, 150f)
        };

        public override bool ShouldShowFor(StatRequest req)
        {
            return req.Def is ThingDef thingDef && thingDef.HasComp<CompAbnormalityHolder>();
        }

        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            float baseValue = base.GetValueUnfinalized(req, applyPostProcess);
            if (!req.HasThing || req.Thing.MapHeld == null)
            {
                return baseValue;
            }

            Room room = req.Thing.GetRoom();
            if (room == null || room.TouchesMapEdge)
            {
                return baseValue;
            }

            Map map = req.Thing.MapHeld;
            bool outdoors = room.PsychologicallyOutdoors;
            float lighting = 0f;
            float floorStrength = 0f;
            int floorCells = 0;

            foreach (IntVec3 cell in room.Cells)
            {
                if (!outdoors)
                {
                    lighting += map.glowGrid.GroundGlowAt(cell);
                }

                if (cell.InBounds(map) && !cell.Filled(map))
                {
                    floorStrength += cell.GetTerrain(map).statBases.GetStatValueFromList(Defs.StatDefOf.LC_ContainmentStrength, 0f);
                    floorCells++;
                }
            }

            lighting = room.CellCount > 0 ? lighting / room.CellCount * 10f : 0f;
            floorStrength = floorCells > 0 ? floorStrength / floorCells : 0f;

            float wallHp = 0f;
            int wallCount = 0;
            foreach (IntVec3 cell in room.BorderCellsCached.Distinct())
            {
                Building building = cell.InBounds(map) ? cell.GetEdifice(map) : null;
                if (building != null && !(building is Building_Door))
                {
                    wallHp += building.HitPoints;
                    wallCount++;
                }
            }

            float wallStrength = wallCount > 0 ? WallStrengthFromHp.Evaluate(wallHp / wallCount) : 0f;
            var doors = room.ContainedAndAdjacentThings.OfType<Building_Door>().Distinct().ToList();
            float doorStrength = doors.Count > 0 ? doors.Average(door => door.HitPoints) / 5f : 0f;
            int otherPlatforms = room.ContainedAndAdjacentThings.Count(thing => thing != req.Thing && thing.HasComp<CompAbnormalityHolder>());
            float roofOffset = !outdoors && room.OpenRoofCount > 0 ? -30f : 0f;
            float strength = baseValue + lighting + floorStrength + wallStrength + doorStrength + roofOffset;

            for (int i = 0; i < otherPlatforms; i++)
            {
                strength *= 0.9f;
            }

            CompAbnormalityHolder holder = req.Thing.TryGetComp<CompAbnormalityHolder>();
            strength *= holder?.Props.containmentFactor ?? 1f;
            return Mathf.Max(0f, strength);
        }
    }
}
