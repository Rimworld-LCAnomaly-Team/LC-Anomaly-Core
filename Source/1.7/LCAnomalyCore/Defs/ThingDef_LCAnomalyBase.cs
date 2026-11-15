using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// LC异想体ThingDef基类
    /// </summary>
    public abstract class ThingDef_AbnormalityBase : ThingDef
    {

    }

    /// <summary>
    /// LC异想体PawnKind基类
    /// </summary>
    public abstract class PawnKindDef_AbnormalityBase : PawnKindDef
    {

    }

    /// <summary>
    /// LC异想体PawnKind基类（活动实体类）
    /// </summary>
    public class PawnKindDef_AbnormalityEntity : PawnKindDef_AbnormalityBase
    {

    }

    /// <summary>表示 <c>ThingDef_AbnormalityEntity</c> 类型。</summary>
    public abstract class ThingDef_AbnormalityEntity : ThingDef_AbnormalityBase
    {

    }

    /// <summary>
    /// LC异想体ThingDef基类（蛋类）
    /// </summary>
    public class ThingDef_AbnormalityEgg : ThingDef_AbnormalityBase
    {
    }

    /// <summary>
    /// LC异想体ThingDef基类（工具类）
    /// </summary>
    public class ThingDef_AbnormalityTool : ThingDef_AbnormalityBase
    {
    }

    /// <summary>
    /// LC异想体ThingDef基类（生成特效类）
    /// </summary>
    public class ThingDef_AbnormalityEntity_Spawn : ThingDef_AbnormalityBase
    {
    }

    /// <summary>
    /// LC异想体（大罪生物）（WIP）
    /// </summary>
    public class PawnKindDef_AnomalyEntity_SevenSin : PawnKindDef_AbnormalityEntity
    {
    }
}