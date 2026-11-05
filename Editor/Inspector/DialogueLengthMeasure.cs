using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// How long a dialogue takes when it goes on by itself, as the project has it: the graph of the imported dialogue and the recordings in its audio tables.
    /// The recordings of all languages are looked at and the longest of each line counts, so a length that fits every language is given.
    /// </summary>
    public static class DialogueLengthMeasure
    {
        /// <summary>Null when the project has no such dialogue (not synced yet, or the id is wrong).</summary>
        public static DialogueLength? Measure(Guid dialogueId, DialogueAutoAdvance mode, float seconds)
        {
            var graph = LoadGraph(dialogueId);
            if (graph == null) return null;
            var collection = LocalizationEditorSettings.GetAssetTableCollection(LoremanNames.AudioTable(dialogueId));
            return DialogueLengthEstimator.Estimate(graph, mode, seconds, key => LongestVoice(collection, key));
        }

        /// <summary>The graph of the imported dialogue, or null.</summary>
        public static DialogueGraph LoadGraph(Guid dialogueId)
        {
            if (dialogueId == Guid.Empty) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.Id == dialogueId) return DialogueGraphReader.Read(asset.GraphBytes);
            }
            return null;
        }

        private static double LongestVoice(AssetTableCollection collection, TextKey key)
        {
            if (collection == null || key.IsNone) return 0d;
            var longest = 0d;
            foreach (var table in collection.AssetTables)
            {
                var entry = table.GetEntry(key.Id);
                if (entry == null || string.IsNullOrEmpty(entry.Guid)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(entry.Guid));
                if (clip != null) longest = Math.Max(longest, clip.length);
            }
            return longest;
        }
    }
}
