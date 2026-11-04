using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// For build machines: <c>Unity -batchmode -quit -projectPath … -executeMethod Loreman.Dialogues.Editor.LoremanCli.SyncAll</c> syncs every .lorebuild of the project
    /// (so the generated assets need not be committed) and exits with 1 if any of them fails.
    /// </summary>
    public static class LoremanCli
    {
        public static void SyncAll() => Run(force: false);

        public static void ForceSyncAll() => Run(force: true);

        private static void Run(bool force)
        {
            var builds = LoreBuildSync.FindBuilds();
            if (builds.Length == 0) Debug.LogWarning("Loreman: no .lorebuild file in the Assets folder.");
            var failed = false;
            foreach (var path in builds)
                if (LoreBuildSync.SyncAsset(path, force) == null) failed = true;
            AssetDatabase.SaveAssets();
            if (failed) EditorApplication.Exit(1);
        }
    }
}
