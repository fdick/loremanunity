using System;
using System.Collections.Generic;
using System.Linq;
using Loreman.Items;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Loreman.Dialogues.Editor
{
    /// <summary>Finds the items the importer made, with their names (in the language of the project) and the places of their pictures, for the pickers and the window.</summary>
    public static class ItemLocator
    {
        private static List<ItemEntry> _cache;
        private static double _cachedAt;

        /// <summary>Forgets the list: something was imported. (A field of an inspector asks for it on every repaint, so it is kept for a moment.)</summary>
        public static void Invalidate() => _cache = null;

        /// <summary>The items of the project, sorted (see <see cref="ItemSearch.Sort"/>). The list is the same object for a second, then it is made again.</summary>
        public static List<ItemEntry> FindAll()
        {
            if (_cache != null && EditorApplication.timeSinceStartup - _cachedAt < 1.0) return _cache;
            _cache = Read();
            _cachedAt = EditorApplication.timeSinceStartup;
            return _cache;
        }

        private static List<ItemEntry> Read()
        {
            var result = new List<ItemEntry>();
            var map = LoremanSettings.Find()?.LocaleMap();
            foreach (var guid in AssetDatabase.FindAssets("t:ItemCatalog"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(path);
                if (catalog == null || catalog.ProjectGuid == Guid.Empty) continue;
                try
                {
                    Add(result, catalog, path, map);
                }
                catch (DialogueFormatException)
                {
                    // a damaged catalog lists nothing; the next sync makes it again
                }
            }
            return ItemSearch.Sort(result);
        }

        private static void Add(List<ItemEntry> result, ItemCatalog catalog, string catalogPath, IReadOnlyDictionary<string, string> map)
        {
            var data = catalog.Data;
            var collection = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.ItemsTable(catalog.ProjectGuid));
            var code = LocaleCodes.ToUnity(catalog.BaseLanguage, map);
            var table = collection?.StringTables.FirstOrDefault(t => string.Equals(t.LocaleIdentifier.Code, code, StringComparison.OrdinalIgnoreCase))
                        ?? collection?.StringTables.FirstOrDefault();
            string Text(TextKey key) => key.IsNone || table == null ? string.Empty : table.GetEntry(key.Id)?.Value ?? string.Empty;

            var folderPaths = new string[data.Folders.Count];
            for (var i = 0; i < folderPaths.Length; i++)
            {
                var f = data.Folders[i];
                folderPaths[i] = f.Parent >= 0 ? folderPaths[f.Parent] + "/" + f.Name : f.Name;
            }
            var iconsFolder = System.IO.Path.GetDirectoryName(catalogPath).Replace('\\', '/') + "/Icons/";
            foreach (var item in data.Items)
            {
                var name = Text(item.Name);
                result.Add(new ItemEntry(
                    item.Id.ToString("D"), name.Length > 0 ? name : "(unnamed)", item.Folder >= 0 ? folderPaths[item.Folder] : string.Empty, Text(item.Description),
                    item.Icon >= 0 ? iconsFolder + System.IO.Path.GetFileName(data.Icons[item.Icon]) : string.Empty));
            }
        }
    }

    /// <summary>Anything imported may be an item catalog or a table of its words: the list of items is read again.</summary>
    internal sealed class ItemLocatorInvalidator : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) => ItemLocator.Invalidate();
    }
}
