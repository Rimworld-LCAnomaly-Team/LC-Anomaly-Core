using Verse;

namespace LCAnomalyCore.Comp
{
    public abstract class LC_CompProperties_RequireThingSpawner : CompProperties
    {
        public ThingDef thingRequire;

        public ThingDef thingToSpawn;

        public int spawnCount = 1;

        public IntRange spawnIntervalRange = new IntRange(100, 100);

        public int spawnMaxAdjacent = -1;

        public bool spawnForbidden;

        public bool requiresPower;

        public bool writeTimeLeftToSpawn;

        public bool showMessageIfOwned;

        public string saveKeysPrefix;

        public bool inheritFaction;

        public LC_CompProperties_RequireThingSpawner()
        {
            compClass = typeof(LC_CompRequireThingSpawner);
        }
    }
}