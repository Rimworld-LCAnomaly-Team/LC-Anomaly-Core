using LCAnomalyCore.Buildings;
using System.Collections.Generic;
using System.Security.Policy;
using UnityEngine;
using Verse;

namespace LCAnomalyCore.Util
{
    public static class ColorUtil
    {
        public static readonly Texture2D RedTex = SolidColorMaterials.NewSolidColorTexture(Color.red);
        public static readonly Texture2D WhiteTex = SolidColorMaterials.NewSolidColorTexture(Color.white);
        public static readonly Texture2D PurpleTex = SolidColorMaterials.NewSolidColorTexture(GenColor.FromHex("7e1e9c"));
        public static readonly Texture2D BlueTex = SolidColorMaterials.NewSolidColorTexture(Color.blue);
        public static readonly Texture2D CyanTex = SolidColorMaterials.NewSolidColorTexture(Color.cyan);

        #region 部门配色

        private static readonly Color ControlTeam = new Color(0.431f, 0.352f, 0.217f);
        private static readonly Color InformationTeam = new Color(0.317f, 0.235f, 0.447f);
        private static readonly Color TrainingTeam = new Color(0.483f, 0.305f, 0.212f);
        private static readonly Color SafetyTeam = new Color(0.34f, 0.45f, 0.21f);

        private static readonly Color CentralCommandTeam = new Color(0.427f, 0.365f, 0.209f);
        private static readonly Color WelfareTeam = new Color(0.18f, 0.284f, 0.536f);
        private static readonly Color DisciplinaryTeam = new Color(0.617f, 0.192f, 0.192f);

        private static readonly Color RecordTeam = new Color(0.333f, 0.333f, 0.333f);
        private static readonly Color ExtractionTeam = new Color(0.466f, 0.402f, 0.132f);
        private static readonly Color ArchitectureTeam = new Color(0.333f, 0.333f, 0.333f);

        /// <summary>
        /// 部门配色字典
        /// </summary>
        public static readonly Dictionary<EDepartmentType, Color> TeamStyleColorDict = new Dictionary<EDepartmentType, Color>()
        {
            { EDepartmentType.ControlTeam, ControlTeam },
            { EDepartmentType.InformationTeam, InformationTeam },
            { EDepartmentType.TrainingTeam, TrainingTeam },
            { EDepartmentType.SafetyTeam, SafetyTeam },

            { EDepartmentType.CentralCommandTeam, CentralCommandTeam },
            { EDepartmentType.WelfareTeam, WelfareTeam },
            { EDepartmentType.DisciplinaryTeam, DisciplinaryTeam },

            { EDepartmentType.RecordTeam, RecordTeam },
            { EDepartmentType.ExtractionTeam, ExtractionTeam },
            { EDepartmentType.ArchitectureTeam, ArchitectureTeam },
        };

        #endregion
    }
}