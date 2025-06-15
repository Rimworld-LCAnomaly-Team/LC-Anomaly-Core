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
                    CachedTopGraphic.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfHoldingPlatform + "QliphothIndicator/Top" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                LogUtil.Message("QliphothIndicatorTex initialized");
            }

            return CachedTopGraphic;
        }

        #endregion 逆卡巴拉计数器

        #region 独立PeBox计数器

        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_Single = new List<Graphic>();

        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_DoubleLeft = new List<Graphic>();
        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_DoubleRight = new List<Graphic>();

        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_ThirdLeft = new List<Graphic>();
        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_ThirdMid = new List<Graphic>();
        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_ThirdRight = new List<Graphic>();

        private static List<Graphic> CachedTopGraphic_IndiPeBoxIndicator_Mixed = new List<Graphic>();

        private static bool IndiPeBoxIndicator_Initialized = false;

        /// <summary>
        /// 获取PeBoxCounter图集
        /// </summary>
        /// <returns>图集</returns>
        public static List<Graphic> IndiPeBoxIndicator_GetCachedTopGraphic(int amount)
        {
            //初始化
            if (!IndiPeBoxIndicator_Initialized)
            {
                string loc = baseLocOfHoldingPlatform + "PeBoxCounter/";

                for (int i = 0; i < 10; i++)
                {
                    //个位数
                    CachedTopGraphic_IndiPeBoxIndicator_Single.Add(GraphicDatabase.Get<Graphic_Single>(loc + "Single/mid_" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                    //两位数右侧
                    CachedTopGraphic_IndiPeBoxIndicator_DoubleRight.Add(GraphicDatabase.Get<Graphic_Single>(loc + "Double/right_" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                    //三位数中间
                    CachedTopGraphic_IndiPeBoxIndicator_ThirdMid.Add(GraphicDatabase.Get<Graphic_Single>(loc + "Third/mid_" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                    //三位数右侧
                    CachedTopGraphic_IndiPeBoxIndicator_ThirdRight.Add(GraphicDatabase.Get<Graphic_Single>(loc + "Third/right_" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                }

                for (int i = 1; i < 10; i++)
                {
                    //两位数左侧
                    CachedTopGraphic_IndiPeBoxIndicator_DoubleLeft.Add(GraphicDatabase.Get<Graphic_Single>(loc + "Double/left_" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                    //三位数左侧
                    CachedTopGraphic_IndiPeBoxIndicator_ThirdLeft.Add(GraphicDatabase.Get<Graphic_Single>(loc + "Third/left_" + i,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                }

                LogUtil.Warning("IndiPeBoxIndicator Graphic Initialized");
                IndiPeBoxIndicator_Initialized = true;
            }

            CachedTopGraphic_IndiPeBoxIndicator_Mixed.Clear();

            if (amount < 10)
            {
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_Single[amount]);
            }
            else if (amount < 100)
            {
                //取模个位
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_DoubleRight[amount % 10]);

                //十位
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_DoubleLeft[amount / 10 - 1]);
            }
            else if (amount < 1000)
            {
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_ThirdRight[amount % 10]);

                //取模十位
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_ThirdMid[amount / 10 % 10]);

                //百位
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_ThirdLeft[amount / 100 - 1]);
            }
            else
            {
                //大于999则直接返回999
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_ThirdRight[9]);
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_ThirdMid[9]);
                CachedTopGraphic_IndiPeBoxIndicator_Mixed.Add(CachedTopGraphic_IndiPeBoxIndicator_ThirdLeft[8]);
            }

            return CachedTopGraphic_IndiPeBoxIndicator_Mixed;
        }

        #endregion 独立PeBox计数器

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
            if (CachedGraphic_LevelIndicator.NullOrEmpty())
            {
                string baseLoc = baseLocOfHoldingPlatform + "Level/";
                List<string> list = new List<string>() { "ZAYIN", "TETH", "HE", "WAW", "ALEPH" };

                foreach (string str in list)
                    CachedGraphic_LevelIndicator.Add(str, GraphicDatabase.Get<Graphic_Single>(baseLoc + str,
                        ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                LogUtil.Message("LevelIndicatorTex initialized");
            }

            return CachedGraphic_LevelIndicator[level];
        }

        #endregion 异想体等级显示

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
                if (CachedTopGraphic_EntityNamePlatformTop.MatSingle.mainTexture == null)
                {
                    LogUtil.Warning("EntityNamePlatformTex cant find local language tex, try to use english ver");
                    CachedTopGraphic_EntityNamePlatformTop = GraphicDatabase
                        .Get<Graphic_Single>("UI/HoldingPlatform/" + loc + "_English"
                        , ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white);
                }

                LogUtil.Message("EntityNamePlatformTex initialized");
            }

            return CachedTopGraphic_EntityNamePlatformTop;
        }

        #endregion 收容平台实体名字显示

        #region 收容平台工作类型显示

        private static Dictionary<EAnomalyWorkType, Graphic> CachedTopGraphicDict_WorkTypePlatformTop = new Dictionary<EAnomalyWorkType, Graphic>();

        private static List<EAnomalyWorkType> eAnomalyWorkTypes = new List<EAnomalyWorkType>()
        {
            EAnomalyWorkType.Instinct,
            EAnomalyWorkType.Insight,
            EAnomalyWorkType.Attachment,
            EAnomalyWorkType.Repression
        };

        public static Graphic WorkTypePlatformTopGraphic_Get(EAnomalyWorkType workType)
        {
            //初始化
            if (CachedTopGraphicDict_WorkTypePlatformTop.NullOrEmpty())
            {
                foreach (var type in eAnomalyWorkTypes)
                {
                    var graphic = GraphicDatabase
                        .Get<Graphic_Single>("Things/Building/LC_HoldingPlatform/WorkType/" + type.ToString()
                        , ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white);
                    CachedTopGraphicDict_WorkTypePlatformTop.Add(type, graphic);
                }

                LogUtil.Message("WorkTypePlatformTex initialized");
            }

            return CachedTopGraphicDict_WorkTypePlatformTop[workType];
        }

        #endregion 收容平台工作类型显示

        #region 收容平台工作类型窗体显示

        private static Dictionary<EAnomalyWorkType, Texture2D> CachedTextureDict_DialogAssignWorkTypeNormal = new Dictionary<EAnomalyWorkType, Texture2D>();

        public static Texture2D DialogAssignWorkTypeNormalTexture_Get(EAnomalyWorkType workType)
        {
            //初始化
            if (CachedTextureDict_DialogAssignWorkTypeNormal.NullOrEmpty())
            {
                foreach (var type in eAnomalyWorkTypes)
                {
                    var tex = ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Dialog/" + type.ToString() + "_Normal", true);
                    CachedTextureDict_DialogAssignWorkTypeNormal.Add(type, tex);
                }

                LogUtil.Message("DialogAssignWorkTypeNormalTexture initialized");
            }

            return CachedTextureDict_DialogAssignWorkTypeNormal[workType];
        }

        public static Texture2D DialogAssignWorkTypeOnHoverTexture_Get(EAnomalyWorkType workType)
        {
            //未知类型直接返回未知贴图
            if (workType == EAnomalyWorkType.Unknown)
                return ContentFinder<Texture2D>.Get("UI/Commands/WorkType/Dialog/" + workType.ToString() + "_OnHover", true);

            //非未知类型就按照语言获取贴图
            var tex = ContentFinder<Texture2D>
                .Get("UI/Commands/WorkType/Dialog/" + workType.ToString() + "_OnHover_" + LanguageDatabase.activeLanguage, true);
            //如果贴图为null就切换到英文版
            if (tex == null)
                tex = ContentFinder<Texture2D>
                .Get("UI/Commands/WorkType/Dialog/" + workType.ToString() + "_OnHover_" + "English", true);

            return tex;
        }

        #endregion 收容平台工作类型窗体显示

        #region 收容平台Box显示

        private static Dictionary<int, Graphic> CachedTopGraphicDict_BoxBarUnit_PE = new Dictionary<int, Graphic>();
        private static Dictionary<int, Graphic> CachedTopGraphicDict_BoxBarUnit_NE = new Dictionary<int, Graphic>();

        public static Graphic CachedTopGraphic_BoxBarUnit_Get(string BoxType, int maxNum)
        {
            string baseLoc = baseLocOfHoldingPlatform + "Box/";

            //初始化
            if (CachedTopGraphicDict_BoxBarUnit_PE.NullOrEmpty())
            {
                CachedTopGraphicDict_BoxBarUnit_PE.Add(10, GraphicDatabase.Get<Graphic_Single>(baseLoc + "PEBOX_10", ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                CachedTopGraphicDict_BoxBarUnit_PE.Add(14, GraphicDatabase.Get<Graphic_Single>(baseLoc + "PEBOX_14", ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                CachedTopGraphicDict_BoxBarUnit_PE.Add(16, GraphicDatabase.Get<Graphic_Single>(baseLoc + "PEBOX_16", ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
            }
            if (CachedTopGraphicDict_BoxBarUnit_NE.NullOrEmpty())
            {
                CachedTopGraphicDict_BoxBarUnit_NE.Add(10, GraphicDatabase.Get<Graphic_Single>(baseLoc + "NEBOX_10", ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                CachedTopGraphicDict_BoxBarUnit_NE.Add(14, GraphicDatabase.Get<Graphic_Single>(baseLoc + "NEBOX_14", ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                CachedTopGraphicDict_BoxBarUnit_NE.Add(16, GraphicDatabase.Get<Graphic_Single>(baseLoc + "NEBOX_16", ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
            }

            if (BoxType == "PE")
                return CachedTopGraphicDict_BoxBarUnit_PE[maxNum];
            else
                return CachedTopGraphicDict_BoxBarUnit_NE[maxNum];
        }

        #endregion 收容平台Box显示

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

                    LogUtil.Message("CogitoBucketTex initialized");
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

                    LogUtil.Message("BrainSpinalNerveTex initialized");
                }

                return CogitoBucket_CachedBrainSpinalNerve;
            }

            return null;
        }

        #endregion “水桶”

        #region 部门核心

        private static Vector2 drawSizeOfDepartmentCore = Defs.ThingDefOf.LC_DepartmentCore_ControlTeam.graphicData.drawSize;
        private static string baseLocOfDepartmentCoreOverlay = "Things/Building/DepartmentCore/Overlay/";
        private static string baseLocOfDepartmentCoreBase = "Things/Building/DepartmentCore/";

        /// <summary>
        /// 缓存的部门核心覆盖层图集
        /// </summary>
        private static Dictionary<string, Graphic> cachedTopGraphic_DepartmentCore = new Dictionary<string, Graphic>();

        private static List<Graphic> cachedBaseGraphic_DepartmentCore = new List<Graphic>();

        /// <summary>
        /// 获取部门核心覆盖层
        /// </summary>
        /// <returns>图集</returns>
        public static Dictionary<string, Graphic> DepartmentCore_GetCachedTopGraphic()
        {
            if (cachedTopGraphic_DepartmentCore.Count <= 0)
            {
                cachedTopGraphic_DepartmentCore.Add("ControlTeam", 
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "ControlTeam", 
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("InformationTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "InformationTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("TrainingTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "TrainingTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("SafetyTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "SafetyTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                cachedTopGraphic_DepartmentCore.Add("CentralCommandTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "CentralCommandTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("WelfareTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "WelfareTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("DisciplinaryTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "DisciplinaryTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                cachedTopGraphic_DepartmentCore.Add("RecordTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "RecordTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("ExtractionTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "ExtractionTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedTopGraphic_DepartmentCore.Add("ArchitectureTeam",
                    GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreOverlay + "ArchitectureTeam",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));

                LogUtil.Message("QliphothIndicatorTex initialized");
            }

            return cachedTopGraphic_DepartmentCore;
        }

        public static Graphic DepartmentCore_GetCachedBaseGraphic(float percent)
        {
            if (cachedBaseGraphic_DepartmentCore.Count <= 0)
            {
                cachedBaseGraphic_DepartmentCore.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreBase + "Base_0p",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedBaseGraphic_DepartmentCore.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreBase + "Base_25p",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedBaseGraphic_DepartmentCore.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreBase + "Base_50p",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedBaseGraphic_DepartmentCore.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreBase + "Base_75p",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
                cachedBaseGraphic_DepartmentCore.Add(GraphicDatabase.Get<Graphic_Single>(baseLocOfDepartmentCoreBase + "Base_100p",
                    ShaderDatabase.Transparent, drawSizeOfHoldingPlatform, Color.white));
            }

            if (percent > 0.75f)
                return cachedBaseGraphic_DepartmentCore[4];
            else if (percent > 0.50f)
                return cachedBaseGraphic_DepartmentCore[3];
            else if (percent > 0.25f)
                return cachedBaseGraphic_DepartmentCore[2];
            else if (percent > 0f)
                return cachedBaseGraphic_DepartmentCore[1];
            else
                return cachedBaseGraphic_DepartmentCore[0];
        }

        #endregion 部门核心
    }
}