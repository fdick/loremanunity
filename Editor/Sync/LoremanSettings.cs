using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    [Serializable]
    public sealed class LocaleOverride
    {
        [Tooltip("The code Loreman uses, lower case: pt-br")]
        public string loremanCode;

        [Tooltip("The code of the Unity locale to use for it: pt-BR")]
        public string unityCode;
    }

    /// <summary>
    /// How the importer works. The asset is made the first time a build is imported, in Assets/Loreman; find it with Tools → Loreman → Settings.
    /// </summary>
    public sealed class LoremanSettings : ScriptableObject
    {
        public const string DefaultPath = "Assets/Loreman/LoremanSettings.asset";

        [Tooltip("Where the importer puts the dialogues, the catalog and the string tables it makes. Do not edit these by hand: they are remade from the build.")]
        public string outputFolder = "Assets/Loreman/Generated";

        [Tooltip("The Addressables group of the dialogues and the catalog.")]
        public string addressablesGroup = "Loreman";

        [Tooltip("Every dialogue in a bundle of its own, so loading one dialogue loads only that one. Turn it off to keep them all in one bundle.")]
        public bool packDialoguesSeparately = true;

        [Tooltip("Make the Unity locales that the build has languages for and the project does not. Turn it off to fail instead.")]
        public bool createMissingLocales = true;

        [Header("Voice-overs")]
        [Tooltip("Import the recordings as compact game audio: Vorbis, mono, long ones streamed. Without it Unity keeps its defaults, which are meant for music and sound effects. Applied once, when a recording is first imported; change a clip's own import settings afterwards and they are kept.")]
        public bool optimizeVoiceOvers = true;

        [Range(0.05f, 1f)]
        [Tooltip("Vorbis quality: 0.5 is plenty for speech and about a third of the size of the default 1.0.")]
        public float voiceQuality = 0.5f;

        [Tooltip("Speech needs one channel: it halves the size of a stereo recording.")]
        public bool forceMono = true;

        [Tooltip("A recording bigger than this (KB, as it came from Loreman) is streamed from disk when played instead of being kept in memory.")]
        public int streamLargerThanKB = 500;

        [Header("Languages")]
        [Tooltip("Languages whose Unity locale code is not the obvious one.")]
        public List<LocaleOverride> localeOverrides = new List<LocaleOverride>();

        public IReadOnlyDictionary<string, string> LocaleMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in localeOverrides)
                if (!string.IsNullOrWhiteSpace(o.loremanCode)) map[o.loremanCode.Trim()] = o.unityCode;
            return map;
        }

        /// <summary>The catalogs, one for each Loreman project (builds of different projects live side by side).</summary>
        public string CatalogsFolder => outputFolder.TrimEnd('/') + "/Catalogs";

        public string CatalogPath(Guid projectId) => CatalogsFolder + "/" + projectId.ToString("N") + ".asset";

        /// <summary>Where the first versions kept their only catalog. The importer removes it.</summary>
        public string LegacyCatalogPath => outputFolder.TrimEnd('/') + "/Catalog.asset";

        public string DialoguePath(Guid id) => outputFolder.TrimEnd('/') + "/Dialogues/" + id.ToString("N") + ".asset";

        public string DialoguesFolder => outputFolder.TrimEnd('/') + "/Dialogues";

        public string AudioFolder(Guid dialogueId) => outputFolder.TrimEnd('/') + "/Audio/" + dialogueId.ToString("N");

        public string TablesFolder => outputFolder.TrimEnd('/') + "/Localization";

        public string LocalesFolder => outputFolder.TrimEnd('/') + "/Locales";

        /// <summary>The settings of the project, or null when there are none yet (nothing is made).</summary>
        public static LoremanSettings Find()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LoremanSettings"))
            {
                var found = AssetDatabase.LoadAssetAtPath<LoremanSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The settings of the project; made with the defaults when there are none.</summary>
        public static LoremanSettings GetOrCreate()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:LoremanSettings"))
            {
                var found = AssetDatabase.LoadAssetAtPath<LoremanSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null) return found;
            }
            EnsureFolder(System.IO.Path.GetDirectoryName(DefaultPath).Replace('\\', '/'));
            var settings = CreateInstance<LoremanSettings>();
            AssetDatabase.CreateAsset(settings, DefaultPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>Makes a folder and the ones above it (AssetDatabase makes them one by one).</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
