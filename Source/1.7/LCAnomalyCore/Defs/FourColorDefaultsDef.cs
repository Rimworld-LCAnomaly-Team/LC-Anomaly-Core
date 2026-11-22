using System.Collections.Generic;
using LCAnomalyCore.Damage;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>四色默认抗性表的种族分类。</summary>
    public enum FourColorRaceCategory
    {
        Humanlike,
        Animal,
        Mechanoid,
        Insect
    }

    /// <summary>按种族分类提供的默认四色目标行为与承伤倍率。</summary>
    public class FourColorRaceCategoryDefault
    {
        public FourColorRaceCategory category = FourColorRaceCategory.Humanlike;
        public FourColorTargetKind targetKind = FourColorTargetKind.Default;
        public float red = 1f;
        public float white = 1f;
        public float black = 1f;
        public float pale = 1f;
    }

    /// <summary>按 defName 字符串映射的原版/DLC/三方护甲默认四色等级与抗性。</summary>
    public class FourColorApparelDefault
    {
        public string thingDef;
        public LCRiskLevel riskLevel = LCRiskLevel.TETH;
        public float red = 1f;
        public float white = 1f;
        public float black = 1f;
        public float pale = 1f;
    }

    /// <summary>
    /// Def-driven 默认四色抗性与危险等级映射表（无硬 DLC 依赖）。
    /// </summary>
    public class FourColorDefaultsDef : Def
    {
        public bool enabled = true;
        public List<FourColorRaceCategoryDefault> raceCategories = new List<FourColorRaceCategoryDefault>();
        public List<FourColorApparelDefault> apparelEntries = new List<FourColorApparelDefault>();
    }
}
