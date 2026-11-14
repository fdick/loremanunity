using System;
using System.Collections.Generic;
using System.Linq;

namespace Loreman.Dialogues.Editor
{
    /// <summary>An item as the pickers list it (names are in the language of the project: the editor does not know the player's).</summary>
    public sealed class ItemEntry
    {
        public ItemEntry(string id, string name, string folder, string description, string iconPath)
        {
            Id = id;
            Name = name ?? string.Empty;
            Folder = folder ?? string.Empty;
            Description = description ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>“Weapons/Swords”, or empty at the top level.</summary>
        public string Folder { get; }

        public string Description { get; }

        /// <summary>The asset path of the picture, or empty when the item has none.</summary>
        public string IconPath { get; }

        public bool HasFolder => Folder.Length > 0;

        public string FullName => HasFolder ? Folder + " / " + Name : Name;
    }

    /// <summary>What the item pickers (the inspector field, the window) need to find and order items. Pure, so the search is tested without the editor.</summary>
    public static class ItemSearch
    {
        /// <summary>By folder (the ones at the top level last), then by name, ignoring case.</summary>
        public static List<ItemEntry> Sort(IEnumerable<ItemEntry> entries) =>
            entries
                .OrderBy(e => e.HasFolder ? 0 : 1)
                .ThenBy(e => e.Folder, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>
        /// The items that match what was typed: every word is a part of the name, the folder or the description, in any order. Names that begin with the first word come first,
        /// then names that contain it, then the rest. An empty query gives everything in the order of <see cref="Sort"/>.
        /// </summary>
        public static List<ItemEntry> Filter(IReadOnlyList<ItemEntry> entries, string query)
        {
            var words = (query ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToArray();
            if (words.Length == 0) return entries.ToList();
            return entries
                .Select(e => (Entry: e, Score: Score(e, words)))
                .Where(x => x.Score >= 0)
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Entry.HasFolder ? 0 : 1)
                .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Entry)
                .ToList();
        }

        private static int Score(ItemEntry e, string[] words)
        {
            var name = e.Name.ToLowerInvariant();
            var rest = (e.Folder + " " + e.Description).ToLowerInvariant();
            var score = 0;
            foreach (var word in words)
            {
                if (name.StartsWith(word, StringComparison.Ordinal)) continue;
                if (name.Contains(word)) score += 1;
                else if (rest.Contains(word)) score += 3;
                else return -1;
            }
            return score;
        }

        /// <summary>What a field shows for an id: the name, or a reminder that the project no longer has the item.</summary>
        public static string Label(IReadOnlyList<ItemEntry> entries, string id)
        {
            if (string.IsNullOrEmpty(id)) return "(none)";
            foreach (var e in entries)
                if (string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) return e.FullName;
            return "(missing) " + id;
        }
    }
}
