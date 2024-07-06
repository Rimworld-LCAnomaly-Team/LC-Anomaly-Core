using RimWorld;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该mod的所有HistoryEventDef
    /// </summary>
    [DefOf]
    public static class HistoryEventDefOf
    {
        /// <summary>
        /// 被提取了Cogito
        /// </summary>
        [MayRequireAnomaly]
        public static HistoryEventDef CogitoExtracted;

        /// <summary>
        /// 被注射了Cogito
        /// </summary>
        [MayRequireAnomaly]
        public static HistoryEventDef CogitoInjected;
    }
}