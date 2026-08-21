using UnityEngine;
using UnityEditor;

public class AssetBundleBuilder : Editor
{
    [MenuItem("Custom/Build Asset Bundle")]
    static void BuildAssetBundle()
    {
        string outputPath = "Assets/StreamingAssets";
        if (!AssetDatabase.IsValidFolder(outputPath))
        {
            AssetDatabase.CreateFolder("Assets", "StreamingAssets");
        }

        // Ensure the assetPaths array is not empty
        string[] assetPaths = new string[]
        {
            "Assets/Resources/PartDesc.asset"
        };

        // Create a list to store asset bundle build mappings
        var buildMap = new AssetBundleBuild[1];

        // Populate the build map with the asset paths and bundle names
        buildMap[0].assetNames = assetPaths;
        buildMap[0].assetBundleName = "partdescbundle";

        // Build the asset bundles using the build map
        BuildPipeline.BuildAssetBundles(outputPath, buildMap, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
    }
}
