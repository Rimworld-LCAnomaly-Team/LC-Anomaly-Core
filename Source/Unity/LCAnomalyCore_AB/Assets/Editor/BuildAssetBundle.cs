using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildAssetBundle
{
    [MenuItem("AssetBundleTools/BuildAllAssetBundles")]
    public static void BuildAllAB()
    {
        string strABOutPathDir = string.Empty;

        strABOutPathDir = Application.streamingAssetsPath;

        if(Directory.Exists(strABOutPathDir) == false)
        {
            Directory.CreateDirectory(strABOutPathDir);
        }

        BuildPipeline.BuildAssetBundles(strABOutPathDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
