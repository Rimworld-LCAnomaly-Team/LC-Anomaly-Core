using UnityEngine;
using Verse;

namespace LCAnomalyLibrary.Util
{
    public static class ColorUtil
    {
        public static readonly Texture2D RedTex = SolidColorMaterials.NewSolidColorTexture(Color.red);
        public static readonly Texture2D WhiteTex = SolidColorMaterials.NewSolidColorTexture(Color.white);
        public static readonly Texture2D PurpleTex = SolidColorMaterials.NewSolidColorTexture(GenColor.FromHex("7e1e9c"));
        public static readonly Texture2D BlueTex = SolidColorMaterials.NewSolidColorTexture(Color.blue);
        public static readonly Texture2D CyanTex = SolidColorMaterials.NewSolidColorTexture(Color.cyan);
    }
}
