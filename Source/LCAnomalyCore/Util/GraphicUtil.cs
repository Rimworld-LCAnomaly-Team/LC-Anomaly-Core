using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{


    /// <summary>
    /// 图像工具类
    /// </summary>
    public static class GraphicUtil
    {
        private static Vector2 drawSizeOfHoldingPlatform = Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize;
        private static string baseLocOfHoldingPlatform = "Things/Building/LC_HoldingPlatform/";

        #region 逆卡巴拉计数器

        /// <summary>
        /// 缓存的逆卡巴拉计数器的图集
        /// </summary>
        private static List<Graphic> CachedTopGraphic = new List<Graphic>();

        /// <summary>
        /// 不可用贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_QliphothIndicator_TopNull =
            GraphicDatabase.Get<Graphic_Single>(baseLocOfHoldingPlatform + "QliphothIndicator/TopNull",
            ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white);

        /// <summary>
        /// 最大值贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_QliphothIndicator_TopMax =
            GraphicDatabase.Get<Graphic_Single>(baseLocOfHoldingPlatform + "QliphothIndicator/Top9+",
                ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white);

        /// <summary>
        /// 获取逆卡巴拉计数器的图集
        /// </summary>
        /// <returns>图集</returns>
        public static List<Graphic> QliphothIndicator_GetCachedTopGraphic()
        {
            if (CachedTopGraphic.Empty())
            {
                for (int i = 0; i < 10; i++)
                {
                    Log.Message($"贴图缓存：{baseLocOfHoldingPlatform}QliphothIndicator/Top" + i);

                    CachedTopGraphic.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfHoldingPlatform + "QliphothIndicator/Top" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                }
            }

            return CachedTopGraphic;
        }

        #endregion 逆卡巴拉计数器

        #region 独立PeBox计数器

        /// <summary>
        /// 缓存图集
        /// </summary>
        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator = new List<Graphic>();

        /// <summary>
        /// 最大值贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_IndiPeBoxIndicator_Max =
            GraphicDatabase.Get<Graphic_Single>(baseLocOfHoldingPlatform + "PeBoxCounter/99+",
                ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white);

        /// <summary>
        /// 获取图集
        /// </summary>
        /// <returns>图集</returns>
        public static List<Graphic> IndiPeBoxIndicator_GetCachedTopGraphic()
        {
            if (CachedTopGraphic_IndiPeBoxIndicator.Empty())
            {
                for (int i = 0; i < 100; i++)
                {
                    Log.Message($"贴图缓存：{baseLocOfHoldingPlatform}PeBoxCounter/Top" + i);

                    CachedTopGraphic_IndiPeBoxIndicator.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfHoldingPlatform + "PeBoxCounter/Top" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                }
            }

            return CachedTopGraphic_IndiPeBoxIndicator;
        }

        #endregion

        #region 异想体等级显示

        /// <summary>
        /// 缓存图集（异想体等级显示）
        /// </summary>
        private static Dictionary<string, Graphic> CachedGraphic_LevelIndicator = new Dictionary<string, Graphic>();

        /// <summary>
        /// 获取图集（异想体等级显示）
        /// </summary>
        /// <returns>图集</returns>
        public static Graphic LevelIndicator_GetCachedTopGraphic(string level)
        {
            if(CachedGraphic_LevelIndicator.NullOrEmpty())
            {
                string baseLoc = baseLocOfHoldingPlatform + "Level/";
                List<string> list = new List<string>() { "ZAYIN", "TETH", "HE", "WAW", "ALEPH" };

                foreach (string str in list)
                    CachedGraphic_LevelIndicator.Add(str, GraphicDatabase.Get<Graphic_Single>(baseLoc + str,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
            }

            return CachedGraphic_LevelIndicator[level];
        }

        #endregion

        #region 收容平台实体名字显示

        private static Graphic CachedTopGraphic_EntityNamePlatformTop;

        public static Graphic EntityNamePlatformTopGraphic_Get(string loc, bool forced = false)
        {
            //初始化
            if (CachedTopGraphic_EntityNamePlatformTop == null || forced)
            {
                CachedTopGraphic_EntityNamePlatformTop = GraphicDatabase
                    .Get<Graphic_Single>("UI/HoldingPlatform/" + loc + "_" + LanguageDatabase.activeLanguage
                    , ShaderDatabase.Transparent
                    , drawSizeOfHoldingPlatform, Color.white);

                //如果贴图为null就切换到英文版
                if(CachedTopGraphic_EntityNamePlatformTop.MatSingle.mainTexture == null)
                {
                    CachedTopGraphic_EntityNamePlatformTop = GraphicDatabase
                        .Get<Graphic_Single>("UI/HoldingPlatform/" + loc + "_English"
                        , ShaderDatabase.Transparent
                        , drawSizeOfHoldingPlatform, Color.white);
                }
            }

            return CachedTopGraphic_EntityNamePlatformTop;
        }

        #endregion

        #region “水桶”

        /// <summary>
        /// 缓存的玻璃图
        /// </summary>
        private static Graphic CogitoBucket_CachedGlassGraphic = new Graphic();

        /// <summary>
        /// 缓存的脑-脊髓神经图
        /// </summary>
        private static Graphic CogitoBucket_CachedBrainSpinalNerve = new Graphic();

        /// <summary>
        /// 获取逆卡巴拉计数器的图集
        /// </summary>
        /// <returns>图集</returns>
        public static Graphic CogitoBucket_GetCachedGraphic(string type)
        {
            if (type == "Glass")
            {
                if (CogitoBucket_CachedGlassGraphic.path.NullOrEmpty())
                {
                    CogitoBucket_CachedGlassGraphic =
                        GraphicDatabase.Get<Graphic_Single>("Things/Building/TheBucket/TheBucket_Glass",
                        ShaderDatabase.MoteGlow, Defs.ThingDefOf.CogitoBucket.graphicData.drawSize, Color.white);

                    Log.Message("贴图缓存：Things/Building/TheBucket/TheBucket_Glass");
                }

                return CogitoBucket_CachedGlassGraphic;
            }
            else if (type == "BrainSpinalNerve")
            {
                if (CogitoBucket_CachedBrainSpinalNerve.path.NullOrEmpty())
                {
                    CogitoBucket_CachedBrainSpinalNerve =
                        GraphicDatabase.Get<Graphic_Single>("Things/Building/TheBucket/TheBucket_BrainSpinalNerve",
                        ShaderDatabase.Transparent, Defs.ThingDefOf.CogitoBucket.graphicData.drawSize, Color.white);
                }

                return CogitoBucket_CachedBrainSpinalNerve;
            }

            return null;
        }

        #endregion “水桶”
    }
}