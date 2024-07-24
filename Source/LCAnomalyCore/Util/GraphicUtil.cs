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
        #region 逆卡巴拉计数器

        /// <summary>
        /// 缓存的逆卡巴拉计数器的图集
        /// </summary>
        private static List<Graphic> CachedTopGraphic = new List<Graphic>();

        /// <summary>
        /// 不可用贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_QliphothIndicator_NotAllowed =
            GraphicDatabase.Get<Graphic_Single>("Things/Building/QliphothIndicator/NotAllowed",
            ShaderDatabase.Transparent, Defs.ThingDefOf.QliphothIndicator.graphicData.drawSize, Color.white);

        /// <summary>
        /// 最大值贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_QliphothIndicator_Max =
            GraphicDatabase.Get<Graphic_Single>("Things/Building/QliphothIndicator/9+",
                ShaderDatabase.Transparent, Defs.ThingDefOf.QliphothIndicator.graphicData.drawSize, Color.white);

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
                    Log.Message("贴图缓存：Things/Building/QliphothIndicator/Top" + i);

                    CachedTopGraphic.Add(GraphicDatabase.Get<Graphic_Single>("Things/Building/QliphothIndicator/Top" + i,
                        ShaderDatabase.Transparent, Defs.ThingDefOf.QliphothIndicator.graphicData.drawSize, Color.white));
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
        /// 不可用贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_IndiPeBoxIndicator_NotAllowed = 
            GraphicDatabase.Get<Graphic_Single>("Things/Building/IndiPeBoxIndicator/NotAllowed",
            ShaderDatabase.Transparent, Defs.ThingDefOf.IndiPeBoxIndicator.graphicData.drawSize, Color.white);

        /// <summary>
        /// 最大值贴图
        /// </summary>
        public static readonly Graphic CachedTopGraphic_IndiPeBoxIndicator_Max =
            GraphicDatabase.Get<Graphic_Single>("Things/Building/IndiPeBoxIndicator/99+",
                ShaderDatabase.Transparent, Defs.ThingDefOf.IndiPeBoxIndicator.graphicData.drawSize, Color.white);

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
                    Log.Message("贴图缓存：Things/Building/IndiPeBoxIndicator/Top" + i);

                    CachedTopGraphic_IndiPeBoxIndicator.Add(GraphicDatabase.Get<Graphic_Single>("Things/Building/IndiPeBoxIndicator/Top" + i,
                        ShaderDatabase.Transparent, Defs.ThingDefOf.IndiPeBoxIndicator.graphicData.drawSize, Color.white));
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
                string baseLoc = "Things/Building/LC_HoldingPlatform/Level/";
                Vector2 drawSize = Defs.ThingDefOf.LC_HoldingPlatform.graphicData.drawSize;
                List<string> list = new List<string>() { "NULL", "ZAYIN", "TETH", "HE", "WAW", "ALEPH" };

                foreach (string str in list)
                    CachedGraphic_LevelIndicator.Add(str, GraphicDatabase.Get<Graphic_Single>(baseLoc + str,
                        ShaderDatabase.Transparent, drawSize, Color.white));
            }

            return CachedGraphic_LevelIndicator[level];
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