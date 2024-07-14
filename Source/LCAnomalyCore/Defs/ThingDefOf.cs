﻿using LCAnomalyLibrary.Defs;
using RimWorld;
using Verse;

namespace LCAnomalyCore.Defs
{
    /// <summary>
    /// 该mod所有的ThingDef
    /// </summary>
    [DefOf]
    public static class ThingDefOf
    {
        /// <summary>
        /// 通用PeBox
        /// </summary>
        public static ThingDef EnkephalinBox;

        /// <summary>
        /// 脑啡肽（成瘾品）
        /// </summary>
        public static ThingDef Enkephalin;

        /// <summary>
        /// Cogito
        /// </summary>
        public static ThingDef Cogito;

        /// <summary>
        /// 脑与脊髓神经
        /// </summary>
        public static ThingDef BrainSpinalNerve;

        /// <summary>
        /// LC收容单元
        /// </summary>
        public static LC_HoldingPlatformDef LC_HoldingPlatform;

        /// <summary>
        /// 水井（建筑）
        /// </summary>
        public static ThingDef TheWell;

        /// <summary>
        /// 逆卡巴拉计数器（建筑）
        /// </summary>
        public static ThingDef QliphothIndicator;

        /// <summary>
        /// 独立PeBox指示器（建筑）
        /// </summary>
        public static ThingDef IndiPeBoxIndicator;

        /// <summary>
        /// 异想体等级指示器（建筑）
        /// </summary>
        public static ThingDef LevelIndicator;

        /// <summary>
        /// Cogito水桶（建筑）
        /// </summary>
        public static ThingDef CogitoBucket;
    }
}