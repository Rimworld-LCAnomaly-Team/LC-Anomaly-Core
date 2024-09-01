using Verse;

namespace LCAnomalyLibrary.Defs
{
    /// <summary>
    /// LC异想体ThingDef基类
    /// </summary>
    public abstract class ThingDef_LCAnomalyBase : ThingDef
    {
        /// <summary>
        /// 异想体评估等级标签
        /// </summary>
        public readonly string anomalyLevelTag;
    }

    /// <summary>
    /// LC异想体PawnKind基类
    /// </summary>
    public abstract class PawnKindDef_LCAnomalyBase : PawnKindDef
    {
        /// <summary>
        /// 异想体评估等级标签
        /// </summary>
        public readonly string anomalyLevelTag;
    }

    /// <summary>
    /// LC异想体PawnKind基类（活动实体类）
    /// </summary>
    public class ThingDef_AnomalyEntity : PawnKindDef_LCAnomalyBase
    {
    }

    /// <summary>
    /// LC异想体ThingDef基类（蛋类）
    /// </summary>
    public class ThingDef_AnomalyEgg : ThingDef_LCAnomalyBase
    {
    }

    /// <summary>
    /// LC异想体ThingDef基类（工具类）
    /// </summary>
    public class ThingDef_AnomalyTool : ThingDef_LCAnomalyBase
    {
    }

    /// <summary>
    /// LC异想体ThingDef基类（生成特效类）
    /// </summary>
    public class ThingDef_AnomalyEntity_Spawn : ThingDef_LCAnomalyBase
    {
    }

    /// <summary>
    /// LC异想体（大罪生物）（WIP）
    /// </summary>
    public class PawnKindDef_AnomalyEntity_SevenSin : ThingDef_AnomalyEntity
    {
    }
}