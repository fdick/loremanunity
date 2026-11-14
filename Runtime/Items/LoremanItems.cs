using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Loreman.Items
{
    /// <summary>
    /// The one item database a game usually needs, made the first time somebody asks. Nothing to set up:
    /// <code>
    /// var items = await LoremanItems.GetAsync();
    /// inventory.Add(items.Find("Health Potion"), 3);
    /// </code>
    /// A game that wants its own (another loader, a test) makes an <see cref="ItemDatabase"/> itself, or gives this one with <see cref="Use"/>.
    /// </summary>
    public static class LoremanItems
    {
        private static ItemDatabase _shared;
        private static Task<ItemDatabase> _loading;

        /// <summary>The shared database, or null while nobody has asked for it yet. Check <see cref="IsReady"/> before using it without awaiting.</summary>
        public static ItemDatabase Database => _shared;

        public static bool IsReady => _shared != null && _shared.IsReady;

        /// <summary>The shared database, loaded (it is made and loaded the first time; the next calls give the same one).</summary>
        public static Task<ItemDatabase> GetAsync(CancellationToken cancellationToken = default)
        {
            if (_loading == null)
            {
                if (_shared == null) _shared = ItemDatabase.CreateDefault();
                var loading = LoadAsync(_shared, cancellationToken);
                _loading = loading;
                if (loading.IsCompleted && !loading.IsCompletedSuccessfully) _loading = null; // it failed before it could be remembered: the next ask tries again
                return loading;
            }
            return _loading;
        }

        private static async Task<ItemDatabase> LoadAsync(ItemDatabase database, CancellationToken cancellationToken)
        {
            try
            {
                await database.InitializeAsync(cancellationToken);
                return database;
            }
            catch
            {
                _loading = null; // the next ask tries again
                throw;
            }
        }

        /// <summary>Uses this database as the shared one (a game with its own loader, a test). The old one is disposed.</summary>
        public static void Use(ItemDatabase database)
        {
            if (!ReferenceEquals(_shared, database)) _shared?.Dispose();
            _shared = database;
            _loading = null;
        }

        /// <summary>Lets the shared database go (it is made again when asked for).</summary>
        public static void Release()
        {
            _shared?.Dispose();
            _shared = null;
            _loading = null;
        }

        // With "Enter Play Mode Options" (no domain reload) statics survive between runs: a database of the last run points at assets that are gone.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _shared = null;
            _loading = null;
        }
    }
}
