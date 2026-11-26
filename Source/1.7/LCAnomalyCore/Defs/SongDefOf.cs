using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>表示 <c>SongDefOf</c> 类型。</summary>
    [DefOf]
    public static class SongDefOf
    {
        /// <summary>
        /// 一级警报
        /// </summary>
        public static SongDef FirstWarning;

        /// <summary>
        /// 二级警报
        /// </summary>
        public static SongDef SecondWarning;

        /// <summary>
        /// 三级警报
        /// </summary>
        public static SongDef ThirdWarning;

        /// <summary>
        /// 异想体三选一提取界面 BGM (Never Frozen Bottom Flows)
        /// </summary>
        public static SongDef Anomaly_Extract;

        static SongDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SongDefOf));
        }
    }
}