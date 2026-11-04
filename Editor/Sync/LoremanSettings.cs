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

        [Tooltip("Languages whose Unity locale code is not the obvious one.")]
        public List<LocaleOverride> localeOverrides = new List<LocaleOverride>();

        public IReadOnlyDictionary<string, string> LocaleMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in localeOverrides)
                if (!string.IsNullOrWhiteSpace(o.loremanCode)) map[o.loremanCode.Trim()] = o.unityCode;
            return map;
        }

        public string CatalogPath => outputFolder.TrimEnd('/') + "/Catalog.asset";

        public string DialoguePath(Guid id) => outputFolder.TrimEnd('/') + "/Dialogues/" + id.ToString("N") + ".asset";

        public string DialoguesFolder => outputFolder.TrimEnd('/') + "/Dialogues";

        public string AudioFolder(Guid dialogueId) => outputFolder.TrimEnd('/') + "/Audio/" + dialogueId.ToString("N");

        public string TablesFolder => outputFolder.TrimEnd('/') + "/Localization";

        public string LocalesFolder => outputFolder.TrimEnd('/') + "/Locales";

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
