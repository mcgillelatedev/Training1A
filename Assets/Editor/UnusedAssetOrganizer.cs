using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class UnusedAssetOrganizer : EditorWindow
{
    private const string UnusedRoot = "Assets/_Unused";

    /*
     * Add the root folders containing third-party assets in your project.
     *
     * A folder listed here is protected from being moved. Its dependencies
     * are also treated as used, preserving the package as a unit.
     *
     * Paths must use forward slashes and begin with "Assets/".
     */
    private static readonly string[] ProtectedThirdPartyFolders =
    {
        "Assets/ThirdParty",

        // Examples based on current project:
        "Assets/SteamVR",
        "Assets/SteamVR_Input",
        "Assets/SteamVR_Resources",
        "Assets/StarterAssets",
        "Assets/TextMesh Pro",
        "Assets/XR",
        "Assets/XRI",
        "Assets/Mini First Person Controller",
        "Assets/PBS Materials Variety Pack",
        "Assets/Granite Rocks",
        "Assets/Industrial_lights",
        "Assets/Kevin Iglesias",
        "Assets/DungeonCharacters"
    };

    /*
     * These folder names are protected anywhere beneath Assets.
     *
     * For example:
     * Assets/Resources
     * Assets/MyFeature/Resources
     */
    private static readonly string[] ProtectedFolderNames =
    {
        "Resources",
        "StreamingAssets",
        "AssetBundles"
    };

    private readonly List<string> unusedAssets = new();
    private Vector2 scrollPosition;
    private string status = "No scan has been performed.";
    private bool hasScanResults;

    [MenuItem("Tools/Project Cleanup/Unused Asset Organizer")]
    public static void OpenWindow()
    {
        var window = GetWindow<UnusedAssetOrganizer>();
        window.titleContent = new GUIContent("Unused Assets");
        window.minSize = new Vector2(700, 450);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Unused Asset Organizer",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "This is intentionally conservative. Every scene, prefab, " +
            "ScriptableObject, protected folder, and all of their recursive " +
            "dependencies will be kept.",
            MessageType.Info);

        EditorGUILayout.HelpBox(
            "Commit the project to version control or make a backup before " +
            "moving anything. String-based loading, custom file paths, " +
            "reflection, and unusual import systems cannot always be detected.",
            MessageType.Warning);

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. Preview Unused Assets", GUILayout.Height(32)))
            {
                PreviewUnusedAssets();
            }

            using (new EditorGUI.DisabledScope(!hasScanResults || unusedAssets.Count == 0))
            {
                if (GUILayout.Button("2. Move to Assets/_Unused", GUILayout.Height(32)))
                {
                    ConfirmAndMoveAssets();
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        if (!hasScanResults)
        {
            return;
        }

        EditorGUILayout.LabelField(
            $"Potentially unused assets: {unusedAssets.Count}",
            EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (string path in unusedAssets)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(
                    path,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Select", GUILayout.Width(55)))
                {
                    SelectAsset(path);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void PreviewUnusedAssets()
    {
        unusedAssets.Clear();
        hasScanResults = false;

        try
        {
            status = "Scanning project...";
            Repaint();

            HashSet<string> allAssets = FindAllCandidateAssets();
            HashSet<string> roots = FindDependencyRoots();
            HashSet<string> usedAssets = FindRecursiveDependencies(roots);

            foreach (string assetPath in allAssets)
            {
                if (usedAssets.Contains(assetPath))
                {
                    continue;
                }

                if (IsProtectedPath(assetPath))
                {
                    continue;
                }

                unusedAssets.Add(assetPath);
            }

            unusedAssets.Sort(StringComparer.OrdinalIgnoreCase);
            hasScanResults = true;

            status =
                $"Scan complete. Examined {allAssets.Count:N0} candidate assets, " +
                $"found {roots.Count:N0} dependency roots, and identified " +
                $"{unusedAssets.Count:N0} potentially unused assets.";

            Debug.Log(
                $"Unused Asset Organizer: {unusedAssets.Count} candidates found.");
        }
        catch (Exception exception)
        {
            status = "Scan failed. See the Console for details.";
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    private static HashSet<string> FindAllCandidateAssets()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Object",
            new[] { "Assets" });

        var results = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = NormalizePath(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            EditorUtility.DisplayProgressBar(
                "Finding project assets",
                path,
                SafeProgress(i, guids.Length));

            if (!IsMovableAsset(path))
            {
                continue;
            }

            results.Add(path);
        }

        return results;
    }

    private static HashSet<string> FindDependencyRoots()
    {
        var roots = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        AddAssetsMatchingFilter(roots, "t:Scene");
        AddAssetsMatchingFilter(roots, "t:Prefab");
        AddAssetsMatchingFilter(roots, "t:ScriptableObject");

        /*
         * Protected assets become roots as well. This prevents the cleaner
         * from moving dependencies required by a protected package or folder.
         */
        string[] allGuids = AssetDatabase.FindAssets(
            "t:Object",
            new[] { "Assets" });

        for (int i = 0; i < allGuids.Length; i++)
        {
            string path = NormalizePath(
                AssetDatabase.GUIDToAssetPath(allGuids[i]));

            EditorUtility.DisplayProgressBar(
                "Finding protected assets",
                path,
                SafeProgress(i, allGuids.Length));

            if (IsMovableAsset(path) && IsProtectedPath(path))
            {
                roots.Add(path);
            }
        }

        return roots;
    }

    private static void AddAssetsMatchingFilter(
        HashSet<string> destination,
        string filter)
    {
        string[] guids = AssetDatabase.FindAssets(
            filter,
            new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = NormalizePath(
                AssetDatabase.GUIDToAssetPath(guid));

            if (!string.IsNullOrWhiteSpace(path))
            {
                destination.Add(path);
            }
        }
    }

    private static HashSet<string> FindRecursiveDependencies(
        HashSet<string> roots)
    {
        var used = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        string[] rootArray = roots.ToArray();

        /*
         * Using the array overload avoids separately rescanning dependencies
         * for every individual scene, prefab, and ScriptableObject.
         */
        string[] dependencies = AssetDatabase.GetDependencies(
            rootArray,
            true);

        for (int i = 0; i < dependencies.Length; i++)
        {
            string path = NormalizePath(dependencies[i]);

            EditorUtility.DisplayProgressBar(
                "Collecting dependencies",
                path,
                SafeProgress(i, dependencies.Length));

            if (!string.IsNullOrWhiteSpace(path))
            {
                used.Add(path);
            }
        }

        return used;
    }

    private void ConfirmAndMoveAssets()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Move potentially unused assets?",
            $"This will move {unusedAssets.Count:N0} assets into " +
            $"{UnusedRoot}.\n\n" +
            "Their original folder structure will be reproduced beneath " +
            "_Unused.\n\n" +
            "Make sure you have committed or backed up the project first.",
            "Move Assets",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        MoveUnusedAssets();
    }

    private void MoveUnusedAssets()
    {
        int movedCount = 0;
        var failures = new List<string>();

        EnsureAssetFolderExists(UnusedRoot);

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < unusedAssets.Count; i++)
            {
                string sourcePath = unusedAssets[i];

                EditorUtility.DisplayProgressBar(
                    "Moving unused assets",
                    sourcePath,
                    SafeProgress(i, unusedAssets.Count));

                if (!AssetExists(sourcePath))
                {
                    failures.Add($"{sourcePath}: asset no longer exists.");
                    continue;
                }

                string destinationPath =
                    BuildUnusedDestinationPath(sourcePath);

                EnsureAssetFolderExists(
                    NormalizePath(Path.GetDirectoryName(destinationPath)));

                destinationPath =
                    AssetDatabase.GenerateUniqueAssetPath(destinationPath);

                string validationError =
                    AssetDatabase.ValidateMoveAsset(
                        sourcePath,
                        destinationPath);

                if (!string.IsNullOrEmpty(validationError))
                {
                    failures.Add(
                        $"{sourcePath}: {validationError}");
                    continue;
                }

                string moveError =
                    AssetDatabase.MoveAsset(
                        sourcePath,
                        destinationPath);

                if (!string.IsNullOrEmpty(moveError))
                {
                    failures.Add(
                        $"{sourcePath}: {moveError}");
                    continue;
                }

                movedCount++;
            }
        }
        catch (Exception exception)
        {
            failures.Add($"Unexpected error: {exception}");
            Debug.LogException(exception);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

        hasScanResults = false;
        unusedAssets.Clear();

        status =
            $"Moved {movedCount:N0} assets to {UnusedRoot}. " +
            $"{failures.Count:N0} moves failed.";

        if (failures.Count > 0)
        {
            Debug.LogError(
                "Unused Asset Organizer encountered the following failures:\n" +
                string.Join("\n", failures));
        }
        else
        {
            Debug.Log(
                $"Unused Asset Organizer moved {movedCount} assets successfully.");
        }

        EditorUtility.DisplayDialog(
            "Unused asset move complete",
            status +
            "\n\nOpen and test every important scene before deleting _Unused.",
            "OK");

        Repaint();
    }

    private static bool IsMovableAsset(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!path.StartsWith(
                "Assets/",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (AssetDatabase.IsValidFolder(path))
        {
            return false;
        }

        if (path.EndsWith(
                ".meta",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        /*
         * Never move this organizer itself or anything already quarantined.
         */
        if (IsInsideFolder(path, UnusedRoot))
        {
            return false;
        }

        string organizerPath = NormalizePath(
            AssetDatabase.GetAssetPath(
                MonoScript.FromScriptableObject(
                    CreateInstance<UnusedAssetOrganizer>())));

        if (!string.IsNullOrEmpty(organizerPath) &&
            string.Equals(
                path,
                organizerPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsProtectedPath(string path)
    {
        path = NormalizePath(path);

        if (IsInsideFolder(path, UnusedRoot))
        {
            return true;
        }

        foreach (string thirdPartyFolder in ProtectedThirdPartyFolders)
        {
            if (IsInsideFolder(path, thirdPartyFolder))
            {
                return true;
            }
        }

        string[] segments = path.Split('/');

        foreach (string segment in segments)
        {
            if (ProtectedFolderNames.Any(
                    folderName => string.Equals(
                        segment,
                        folderName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        /*
         * Common code and editor infrastructure should not be quarantined
         * merely because Unity cannot see a serialized asset reference.
         */
        if (segments.Any(segment =>
                string.Equals(
                    segment,
                    "Editor",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    segment,
                    "Plugins",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string BuildUnusedDestinationPath(string sourcePath)
    {
        string relativePath = sourcePath.Substring("Assets/".Length);
        return NormalizePath($"{UnusedRoot}/{relativePath}");
    }

    private static void EnsureAssetFolderExists(string folderPath)
    {
        folderPath = NormalizePath(folderPath);

        if (string.IsNullOrWhiteSpace(folderPath) ||
            AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] segments = folderPath.Split('/');

        if (segments.Length == 0 ||
            !string.Equals(
                segments[0],
                "Assets",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Folder must be beneath Assets: {folderPath}");
        }

        string currentPath = "Assets";

        for (int i = 1; i < segments.Length; i++)
        {
            string nextPath = $"{currentPath}/{segments[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    segments[i]);
            }

            currentPath = nextPath;
        }
    }

    private static bool IsInsideFolder(
        string assetPath,
        string folderPath)
    {
        assetPath = NormalizePath(assetPath).TrimEnd('/');
        folderPath = NormalizePath(folderPath).TrimEnd('/');

        return string.Equals(
                   assetPath,
                   folderPath,
                   StringComparison.OrdinalIgnoreCase) ||
               assetPath.StartsWith(
                   folderPath + "/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool AssetExists(string path)
    {
        return AssetDatabase.LoadMainAssetAtPath(path) != null;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path)
            ? string.Empty
            : path.Replace('\\', '/');
    }

    private static float SafeProgress(int index, int count)
    {
        return count <= 0
            ? 1f
            : Mathf.Clamp01((float)index / count);
    }

    private static void SelectAsset(string path)
    {
        UnityEngine.Object asset =
            AssetDatabase.LoadMainAssetAtPath(path);

        if (asset == null)
        {
            return;
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
