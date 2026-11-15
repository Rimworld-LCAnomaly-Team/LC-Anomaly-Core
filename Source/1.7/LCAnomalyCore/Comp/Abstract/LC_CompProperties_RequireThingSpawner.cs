using Verse;

namespace LCAnomalyCore.Comp
{
    /// <summary>表示 <c>LC_CompProperties_RequireThingSpawner</c> 类型。</summary>
    public abstract class LC_CompProperties_RequireThingSpawner : CompProperties
    {
        /// <summary>表示 <c>thingRequire</c>。</summary>
        public ThingDef thingRequire;

        /// <summary>表示 <c>thingToSpawn</c>。</summary>
        public ThingDef thingToSpawn;

        /// <summary>表示 <c>spawnCount</c>。</summary>
        public int spawnCount = 1;

        /// <summary>表示 <c>spawnIntervalRange</c>。</summary>
        public IntRange spawnIntervalRange = new IntRange(100, 100);

        /// <summary>表示 <c>spawnMaxAdjacent</c>。</summary>
        public int spawnMaxAdjacent = -1;

        /// <summary>表示 <c>spawnForbidden</c>。</summary>
        public bool spawnForbidden;

        /// <summary>表示 <c>requiresPower</c>。</summary>
        public bool requiresPower;

        /// <summary>表示 <c>writeTimeLeftToSpawn</c>。</summary>
        public bool writeTimeLeftToSpawn;

        /// <summary>表示 <c>showMessageIfOwned</c>。</summary>
        public bool showMessageIfOwned;

        /// <summary>表示 <c>saveKeysPrefix</c>。</summary>
        public string saveKeysPrefix;

        /// <summary>表示 <c>inheritFaction</c>。</summary>
        public bool inheritFaction;

        /// <summary>初始化 <c>LC_CompProperties_RequireThingSpawner</c> 类的新实例。</summary>
        public LC_CompProperties_RequireThingSpawner()
        {
            compClass = typeof(LC_CompRequireThingSpawner);
        }
    }
}