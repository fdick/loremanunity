using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loreman.Dialogues
{
    /// <summary>A hold on a loaded dialogue. The dialogue stays in memory while somebody holds it; dispose the lease to let it go.</summary>
    public sealed class DialogueLease : IDisposable
    {
        private readonly DialogueLibrary _library;
        private bool _disposed;

        internal DialogueLease(DialogueLibrary library, DialogueGraph graph)
        {
            _library = library;
            Graph = graph;
        }

        public DialogueGraph Graph { get; }

        public Guid Id => Graph.Id;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _library.Release(Graph.Id);
        }
    }

    /// <summary>
    /// Loads dialogue graphs when they are asked for and unloads them when nobody holds them. A dialogue asked for twice is loaded once (even when the second
    /// request comes while the first is still loading) and kept until every lease is disposed. Knows nothing about texts: see <see cref="IDialogueTextSource"/>.
    /// </summary>
    public sealed class DialogueLibrary : IDisposable
    {
        private sealed class Entry
        {
            public DialogueAsset Asset;
            public DialogueGraph Graph;
            public int Leases;
            public Task Loading;
        }

        private readonly IAssetLoader _loader;
        private readonly Dictionary<Guid, Entry> _entries = new Dictionary<Guid, Entry>();
        private DialogueCatalog _catalog;
        private bool _disposed;

        public DialogueLibrary(IAssetLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        /// <summary>The catalog, once <see cref="InitializeAsync"/> is done.</summary>
        public DialogueCatalog Catalog => _catalog;

        public bool IsInitialized => _catalog != null;

        /// <summary>How many dialogues are loaded now (what the game holds).</summary>
        public int LoadedCount => _entries.Count;

        public bool IsLoaded(Guid dialogueId) => _entries.TryGetValue(dialogueId, out var e) && e.Graph != null;

        /// <summary>Loads the catalog. Call it once, before anything else.</summary>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (_catalog != null) return;
            _catalog = await _loader.LoadAsync<DialogueCatalog>(LoremanNames.CatalogAddress, cancellationToken);
        }

        /// <summary>Loads a dialogue (or takes the one that is already loaded) and holds it until the lease is disposed.</summary>
        public async Task<DialogueLease> AcquireAsync(Guid dialogueId, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (_catalog == null) throw new InvalidOperationException("Loreman: the library is not initialized. Await InitializeAsync first.");
            if (!_catalog.TryGetDialogue(dialogueId, out _)) throw new KeyNotFoundException($"Loreman: the build has no dialogue {dialogueId}.");

            if (!_entries.TryGetValue(dialogueId, out var entry))
            {
                entry = new Entry();
                _entries[dialogueId] = entry;
                entry.Loading = LoadAsync(dialogueId, entry, cancellationToken);
            }
            entry.Leases++;

            try
            {
                await entry.Loading;
            }
            catch
            {
                Release(dialogueId);
                throw;
            }
            return new DialogueLease(this, entry.Graph);
        }

        internal void Release(Guid dialogueId)
        {
            if (!_entries.TryGetValue(dialogueId, out var entry)) return;
            if (--entry.Leases > 0) return;
            _entries.Remove(dialogueId);
            // a load that is still going finishes first; it lets go of the asset itself
            if (entry.Asset != null) _loader.Release(entry.Asset);
        }

        private async Task LoadAsync(Guid dialogueId, Entry entry, CancellationToken cancellationToken)
        {
            // The load is shared by everyone who asks, so one caller giving up must not stop it: only the token of the first caller reaches the loader.
            var asset = await _loader.LoadAsync<DialogueAsset>(LoremanNames.DialogueAddress(dialogueId), cancellationToken);
            try
            {
                entry.Graph = DialogueGraphReader.Read(asset.GraphBytes);
            }
            catch
            {
                _loader.Release(asset);
                throw;
            }

            if (!_entries.TryGetValue(dialogueId, out var current) || !ReferenceEquals(current, entry))
            {
                // everybody gave up while it was loading (the entry may even have been made again since)
                _loader.Release(asset);
                return;
            }
            entry.Asset = asset;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var entry in _entries.Values)
                if (entry.Asset != null) _loader.Release(entry.Asset);
            _entries.Clear();
            if (_catalog != null) _loader.Release(_catalog);
            _catalog = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DialogueLibrary));
        }
    }
}
