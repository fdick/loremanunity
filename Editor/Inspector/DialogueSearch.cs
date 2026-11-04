using System;
using System.Collections.Generic;
using System.Linq;

namespace Loreman.Dialogues.Editor
{
    /// <summary>A dialogue as the pickers list it.</summary>
    public sealed class DialogueEntry
    {
        public DialogueEntry(string id, string name, string group, int nodes)
        {
            Id = id;
            Name = name ?? string.Empty;
            Group = group ?? string.Empty;
            Nodes = nodes;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>The name of the group in Loreman, or empty.</summary>
        public string Group { get; }

        public int Nodes { get; }

        public bool HasGroup => Group.Length > 0;

        /// <summary>"Group / Name", or just the name.</summary>
        public string FullName => HasGroup ? Group + " / " + Name : Name;
    }

    /// <summary>
    /// What the dialogue pickers (the inspector field, the window) need to list, find and order dialogues. Pure, so the search is tested without the editor.
    /// </summary>
    public static class DialogueSearch
    {
        /// <summary>The dialogues of a catalog in the order a person looks for them: by group (the ungrouped ones last), then by name, ignoring case.</summary>
        public static List<DialogueEntry> Entries(IReadOnlyList<CatalogGroup> groups, IReadOnlyList<CatalogDialogue> dialogues)
        {
            var names = new Dictionary<string, string>();
            foreach (var g in groups) names[g.id] = g.name;
            return dialogues
                .Select(d => new DialogueEntry(d.id, d.name, !string.IsNullOrEmpty(d.groupId) && names.TryGetValue(d.groupId, out var n) ? n : string.Empty, d.nodes))
                .OrderBy(e => e.HasGroup ? 0 : 1)
                .ThenBy(e => e.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<DialogueEntry> Entries(DialogueCatalog catalog) => Entries(catalog.Groups, catalog.Dialogues);

        /// <summary>
        /// The dialogues of every catalog in the project. With more than one build the name of the project goes before the group, so two "Intro" dialogues of
        /// two projects can be told apart.
        /// </summary>
        public static List<DialogueEntry> Entries(IReadOnlyList<DialogueCatalog> catalogs)
        {
            if (catalogs.Count == 1) return Entries(catalogs[0]);
            var all = new List<DialogueEntry>();
            foreach (var catalog in catalogs)
            {
                var project = string.IsNullOrWhiteSpace(catalog.ProjectName) ? "Project" : catalog.ProjectName;
                foreach (var e in Entries(catalog))
                    all.Add(new DialogueEntry(e.Id, e.Name, e.HasGroup ? project + " / " + e.Group : project, e.Nodes));
            }
            return all
                .OrderBy(e => e.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// The entries that match what was typed, best first. Every word of the query must be somewhere in "group name"; ignoring case.
        /// A name that begins with the query comes first, then a name that has all the words, then the ones that match only through the group.
        /// An empty query keeps everything in the order given.
        /// </summary>
        public static List<DialogueEntry> Filter(IReadOnlyList<DialogueEntry> entries, string query)
        {
            var words = Words(query);
            if (words.Length == 0) return entries.ToList();
            var whole = (query ?? string.Empty).Trim();
            return entries
                .Select((e, i) => (Entry: e, Index: i, Score: Score(e, words, whole)))
                .Where(x => x.Score >= 0)
                .OrderBy(x => x.Score).ThenBy(x => x.Index)
                .Select(x => x.Entry)
                .ToList();
        }

        /// <summary>True when the dialogue matches the query (see <see cref="Filter"/>).</summary>
        public static bool Matches(DialogueEntry entry, string query)
        {
            var words = Words(query);
            return words.Length == 0 || Score(entry, words, (query ?? string.Empty).Trim()) >= 0;
        }

        /// <summary>What to show for the id kept in a field: "(none)", "Group / Name", or "(missing) id" for a dialogue the catalog no longer has.</summary>
        public static string Label(IReadOnlyList<DialogueEntry> entries, string id)
        {
            if (string.IsNullOrEmpty(id)) return "(none)";
            var found = entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
            return found != null ? found.FullName : "(missing) " + id;
        }

        private static string[] Words(string query) =>
            (query ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        private static int Score(DialogueEntry e, string[] words, string whole)
        {
            if (!words.All(w => e.FullName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)) return -1;
            if (e.Name.StartsWith(whole, StringComparison.OrdinalIgnoreCase)) return 0;
            if (words.All(w => e.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)) return 1;
            return 2;
        }
    }
}
