using System.IO;
using UnityEngine;

namespace LCAnomalyCore.Util
{
    public static class FileUtil
    {

        /// <summary>
        /// 保存RenderTexture对象到本地png文件
        /// </summary>
        /// <param name="tex">贴图</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileName">文件名</param>
        public static void RenderTexture_Save2Local(RenderTexture tex, string filePath, string fileName)
        {
            string contents = filePath;
            string pngName = fileName;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tex;
            Texture2D png = new Texture2D(tex.width, tex.height, TextureFormat.ARGB32, false);
            png.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            byte[] bytes = png.EncodeToPNG();
            if (!Directory.Exists(contents))
                Directory.CreateDirectory(contents);
            FileStream file = File.Open(contents + "/" + pngName + ".png", FileMode.Create);
            BinaryWriter writer = new BinaryWriter(file);
            writer.Write(bytes);
            file.Close();
            Texture2D.DestroyImmediate(png);
            png = null;
            RenderTexture.active = prev;
        }
    }
}
