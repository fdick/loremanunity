using System;
using System.Collections.Generic;
using System.Linq;

namespace Loreman.Dialogues.Editor
{
    /// <summary>What a sync has to do with the dialogues: make the new ones, remake the changed ones, delete the ones that are gone.</summary>
    public sealed class SyncPlan
    {
        public SyncPlan(IReadOnlyList<ManifestDialogue> toAdd, IReadOnlyList<ManifestDialogue> toUpdate, IReadOnlyList<Guid> toRemove, int unchanged)
        {
            ToAdd = toAdd;
            ToUpdate = toUpdate;
            ToRemove = toRemove;
            Unchanged = unchanged;
        }

        public IReadOnlyList<ManifestDialogue> ToAdd { get; }

        public IReadOnlyList<ManifestDialogue> ToUpdate { get; }

        public IReadOnlyList<Guid> ToRemove { get; }

        public int Unchanged { get; }

        public bool IsEmpty => ToAdd.Count == 0 && ToUpdate.Count == 0 && ToRemove.Count == 0;

        public IEnumerable<ManifestDialogue> ToWrite => ToAdd.Concat(ToUpdate);

        /// <summary>
        /// Compares the build with what the project has: <paramref name="existing"/> are the dialogues whose assets are there now, with the code of the build they came from.
        /// A dialogue is remade only when its code changed, so syncing a build that barely changed is quick and touches few files.
        /// </summary>
        public static SyncPlan Compute(IReadOnlyList<ManifestDialogue> wanted, IReadOnlyDictionary<Guid, string> existing)
        {
            var add = new List<ManifestDialogue>();
            var update = new List<ManifestDialogue>();
            var unchanged = 0;
            var seen = new HashSet<Guid>();
            foreach (var d in wanted)
            {
                var id = d.Guid;
                if (id == Guid.Empty || !seen.Add(id)) continue;
                if (!existing.TryGetValue(id, out var hash)) add.Add(d);
                else if (hash != d.hash) update.Add(d);
                else unchanged++;
            }
            var remove = existing.Keys.Where(id => !seen.Contains(id)).OrderBy(id => id).ToList();
            return new SyncPlan(add, update, remove, unchanged);
        }
    }
}
