using System.Collections.Generic;
using LCAnomalyCore.Damage;
using Verse;

namespace LCAnomalyCore.ModExtensions
{
    /// <summary>表示 <c>FourColorDamageExtension</c> 类型。</summary>
    public class FourColorDamageExtension : DefModExtension
    {
        /// <summary>表示 <c>damageType</c>。</summary>
        public FourColorDamageType damageType;
        /// <summary>表示 <c>riskLevel</c>。</summary>
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
    }

    /// <summary>表示 <c>FourColorWeaponExtension</c> 类型。</summary>
    public class FourColorWeaponExtension : DefModExtension
    {
        /// <summary>表示 <c>damageType</c>。</summary>
        public FourColorDamageType damageType;
        /// <summary>可选的第二四色伤害类型（如圣宣的白/黑双伤）。</summary>
        public FourColorDamageType secondaryDamageType = FourColorDamageType.None;
        /// <summary>表示 <c>riskLevel</c>。</summary>
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        /// <summary>表示 <c>damageMultiplier</c>。</summary>
        public float damageMultiplier = 1f;
        /// <summary>若为 true，则该武器攻击无视掩体、天气与精度惩罚，必定命中目标。</summary>
        public bool guaranteedHit = false;
    }

    /// <summary>表示 <c>FourColorApparelExtension</c> 类型。</summary>
    public class FourColorApparelExtension : DefModExtension
    {
        /// <summary>表示 <c>riskLevel</c>。</summary>
        public LCRiskLevel riskLevel = LCRiskLevel.ZAYIN;
        /// <summary>表示 <c>redResistance</c>。</summary>
        public float redResistance = 1f;
        /// <summary>表示 <c>whiteResistance</c>。</summary>
        public float whiteResistance = 1f;
        /// <summary>表示 <c>blackResistance</c>。</summary>
        public float blackResistance = 1f;
        /// <summary>表示 <c>paleResistance</c>。</summary>
        public float paleResistance = 1f;

        /// <summary>执行 <c>ResistanceFor</c> 定义的操作。</summary>
        public float ResistanceFor(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return redResistance;
                case FourColorDamageType.White: return whiteResistance;
                case FourColorDamageType.Black: return blackResistance;
                case FourColorDamageType.Pale: return paleResistance;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Optional race-level override for compatibility mods.
    /// </summary>
    public class FourColorTargetExtension : DefModExtension
    {
        /// <summary>表示 <c>targetKind</c>。</summary>
        public FourColorTargetKind targetKind = FourColorTargetKind.Default;

        /// <summary>表示 <c>redResistance</c>。</summary>
        public float redResistance = 1f;
        /// <summary>表示 <c>whiteResistance</c>。</summary>
        public float whiteResistance = 1f;
        /// <summary>表示 <c>blackResistance</c>。</summary>
        public float blackResistance = 1f;
        /// <summary>表示 <c>paleResistance</c>。</summary>
        public float paleResistance = 1f;

        /// <summary>返回指定四色伤害类型的承伤倍率。</summary>
        public float ResistanceFor(FourColorDamageType damageType)
        {
            switch (damageType)
            {
                case FourColorDamageType.Red: return redResistance;
                case FourColorDamageType.White: return whiteResistance;
                case FourColorDamageType.Black: return blackResistance;
                case FourColorDamageType.Pale: return paleResistance;
                default: return 1f;
            }
        }
    }

    /// <summary>描述 E.G.O 主动技能的单个伤害阶段，用于在信息面板精确展示总伤害与分段明细。</summary>
    public class FourColorAbilityDamageStage
    {
        /// <summary>在 AbilityDef 的 CompProperties 上反射读取的 FloatRange 或 float 字段名；为空时使用 fallbackRange。</summary>
        public string fieldName;
        /// <summary>该阶段在一轮技能释放中命中的次数。</summary>
        public int hitCount = 1;
        /// <summary>相对字段数值的倍率修正（例如斩击后续段 1.12 倍）。</summary>
        public float damageFactor = 1f;
        /// <summary>当 fieldName 未指定时直接使用的回退伤害范围。</summary>
        public FloatRange fallbackRange = FloatRange.Zero;
        /// <summary>是否按核心最大生命百分比（蓝伤/苍白机制）显示。</summary>
        public bool isPercent = false;
        /// <summary>可选阶段标签翻译键或文本。</summary>
        public string labelKey;
    }

    /// <summary>附加于 AbilityDef 的扩展，声明多段命中次数或专属四色伤害类型。</summary>
    public class FourColorAbilityDamageExtension : DefModExtension
    {
        /// <summary>技能主四色伤害类型；为 None 时继承所属 E.G.O 武器。</summary>
        public FourColorDamageType damageType = FourColorDamageType.None;
        /// <summary>可选第二四色伤害类型（如圣宣死蝶哀歌的白/黑双伤）。</summary>
        public FourColorDamageType secondaryDamageType = FourColorDamageType.None;
        /// <summary>结构化的技能多段伤害清单。</summary>
        public List<FourColorAbilityDamageStage> stages;
    }
}
