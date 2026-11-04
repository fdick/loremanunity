using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>Makes a .lorebuild file an asset of the project (with a summary in the inspector). Turning it into dialogues is <see cref="LoreBuildPostprocessor"/>'s job.</summary>
    [ScriptedImporter(1, "lorebuild")]
    public sealed class LoreBuildImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var reference = ScriptableObject.CreateInstance<LoreBuildReference>();
            try
            {
                using (var file = LoreBuildFile.Open(ctx.assetPath))
                {
                    var m = file.Manifest;
                    reference.projectName = m.projectName;
                    reference.contentHash = m.contentHash;
                    reference.builtAt = m.builtAt;
                    reference.baseLanguage = m.baseLanguage;
                    reference.languages = m.languages.Select(l => l.code).ToArray();
                    reference.dialogueCount = m.dialogues.Count;
                    reference.speakerCount = m.speakers.Count;
                }
            }
            catch (Exception e)
            {
                reference.error = e.Message;
                ctx.LogImportError($"Loreman: {e.Message}");
            }
            ctx.AddObjectToAsset("main", reference);
            ctx.SetMainObject(reference);
        }
    }

    [CustomEditor(typeof(LoreBuildReference))]
    public sealed class LoreBuildReferenceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var r = (LoreBuildReference)target;
            if (!string.IsNullOrEmpty(r.error))
            {
                EditorGUILayout.HelpBox(r.error, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Project", r.projectName);
            EditorGUILayout.LabelField("Built", r.builtAt);
            EditorGUILayout.LabelField("Dialogues", r.dialogueCount.ToString());
            EditorGUILayout.LabelField("Speakers", r.speakerCount.ToString());
            EditorGUILayout.LabelField("Languages", string.Join(", ", r.languages) + "  (base: " + r.baseLanguage + ")");
            EditorGUILayout.LabelField("Content", r.contentHash);
            EditorGUILayout.Space();
            var path = AssetDatabase.GetAssetPath(r);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync")) LoreBuildSync.SyncAsset(path);
                if (GUILayout.Button("Force Sync")) LoreBuildSync.SyncAsset(path, true);
            }
            EditorGUILayout.HelpBox("A build is synced when it is added or replaced. Sync makes the Addressables and Localization tables of the dialogues that changed.", MessageType.None);
        }
    }

    public static class LoremanMenu
    {
        [MenuItem("Tools/Loreman/Sync All Builds")]
        private static void SyncAll()
        {
            var builds = LoreBuildSync.FindBuilds();
            if (builds.Length == 0) Debug.LogWarning("Loreman: no .lorebuild file in the Assets folder. Build the project in Loreman and put the file here.");
            foreach (var path in builds) LoreBuildSync.SyncAsset(path);
        }

        [MenuItem("Tools/Loreman/Force Sync All Builds")]
        private static void ForceSyncAll()
        {
            foreach (var path in LoreBuildSync.FindBuilds()) LoreBuildSync.SyncAsset(path, true);
        }

        [MenuItem("Tools/Loreman/Settings")]
        private static void OpenSettings() => LoremanWindow.Open();

        [MenuItem("Tools/Loreman/Select Settings Asset")]
        private static void SelectSettingsAsset()
        {
            var settings = LoremanSettings.GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
