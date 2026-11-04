using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>Syncs a .lorebuild file of the project into assets (the work behind the menu, the inspector and the importer).</summary>
    public static class LoreBuildSync
    {
        private static bool _running;

        /// <summary>Syncs the build at an asset path ("Assets/…/game.lorebuild"). Errors are logged, not thrown: a bad build must not break the editor.</summary>
        public static SyncReport SyncAsset(string assetPath, bool force = false)
        {
            if (_running) return null;
            _running = true;
            try
            {
                var settings = LoremanSettings.GetOrCreate();
                using (var file = LoreBuildFile.Open(Path.GetFullPath(assetPath)))
                {
                    var report = BuildSynchronizer.Run(file, new UnitySyncTarget(settings), force);
                    Debug.Log($"{report} ({assetPath})");
                    return report;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Loreman: could not sync \"{assetPath}\": {e.Message}");
                Debug.LogException(e);
                return null;
            }
            finally
            {
                _running = false;
            }
        }

        public static string[] FindBuilds()
        {
            var found = Directory.GetFiles(Application.dataPath, "*.lorebuild", SearchOption.AllDirectories);
            for (var i = 0; i < found.Length; i++)
                found[i] = "Assets/" + Path.GetRelativePath(Application.dataPath, found[i]).Replace('\\', '/');
            return found;
        }
    }

    /// <summary>When a .lorebuild is added, replaced or moved in the project, it is synced (after the import is over: assets cannot be made in the middle of it).</summary>
    public sealed class LoreBuildPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var builds = new System.Collections.Generic.List<string>();
            foreach (var path in imported)
                if (path.EndsWith(".lorebuild", StringComparison.OrdinalIgnoreCase)) builds.Add(path);
            foreach (var path in moved)
                if (path.EndsWith(".lorebuild", StringComparison.OrdinalIgnoreCase) && !builds.Contains(path)) builds.Add(path);
            if (builds.Count == 0) return;

            EditorApplication.delayCall += () =>
            {
                foreach (var path in builds)
                    if (File.Exists(path)) LoreBuildSync.SyncAsset(path);
            };
        }
    }
}
