using Verse;

namespace LCAnomalyLibrary.Util
{
    /// <summary>
    /// 曲线工具类
    /// </summary>
    public static class Curves
    {
        /// <summary>
        /// 出逃曲线（和原版一致）
        /// </summary>
        public static readonly SimpleCurve JoinEscapeChanceFromEscapeIntervalCurve =
        [
            new CurvePoint(120f, 0.33f),
            new CurvePoint(60f, 0.5f),
            new CurvePoint(10f, 0.9f)
        ];
    }
}