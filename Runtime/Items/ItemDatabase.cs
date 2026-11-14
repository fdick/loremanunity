using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loreman.Dialogues;
using UnityEngine;

namespace Loreman.Items
{
    /// <summary>
    /// Every item of the game, at hand. It loads the item catalogs of all the Loreman builds in the project once (a few small assets and one string table each) and then answers at once:
    /// <code>
    /// var items = await LoremanItems.GetAsync();           // the shared database, loaded the first time
    /// var sword = items.Find("Iron Sword");                // by name in the current language
    /// var same  = items.Get(swordReference);               // by an ItemReference field of a component
    /// foreach (var item in items.InFolder("Weapons")) ...  // a category
    /// var found = items.Search("heal pot");                // for a search box
    /// </code>
    /// Pictures are loaded when asked for (<see cref="ItemDefinition.LoadIconAsync"/>) and let go when the database is disposed.
    /// </summary>
    public sealed class ItemDatabase : IDisposable
    {
        private readonly IAssetLoader _loader;
        private readonly IItemTextSource _texts;
        private readonly List<ItemCatalog> _catalogs = new List<ItemCatalog>();
        private readonly List<ItemDefinition> _all = new List<ItemDefinition>();
        private readonly Dictionary<Guid, ItemDefinition> _byId = new Dictionary<Guid, ItemDefinition>();
        private readonly List<ItemFolder> _folders = new List<ItemFolder>();
        private readonly Dictionary<string, Task<Sprite>> _iconLoads = new Dictionary<string, Task<Sprite>>();
        private readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Func<ItemDefinition, int> _stackRule = ItemRules.StackOf;
        private Task _initialization;
        private bool _ready;
        private bool _disposed;

        public ItemDatabase(IAssetLoader loader, IItemTextSource texts)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _texts = texts ?? throw new ArgumentNullException(nameof(texts));
            Rules = new DatabaseStackRules(this);
        }

        /// <summary>A database that reads Addressables and Unity Localization (what the importer made).</summary>
        public static ItemDatabase CreateDefault() => new ItemDatabase(new AddressablesAssetLoader(), new LocalizationItemTextSource());

        /// <summary>The items are loaded: ask for them.</summary>
        public bool IsReady => _ready;

        /// <summary>The player changed the language: every name and text is now in the new one (read them again).</summary>
        public event Action TextsChanged;

        /// <summary>Every item of every build in the project.</summary>
        public IReadOnlyList<ItemDefinition> All => _all;

        public int Count => _all.Count;

        /// <summary>The folders (categories) of the items; a parent comes before its children.</summary>
        public IReadOnlyList<ItemFolder> Folders => _folders;

        /// <summary>How many of an item fit in a slot: <see cref="ItemRules.StackOf"/> unless you change it (<see cref="StackRule"/>).</summary>
        public Func<ItemDefinition, int> StackRule
        {
            get => _stackRule;
            set => _stackRule = value ?? ItemRules.StackOf;
        }

        /// <summary>The database as the stack rules of an <see cref="Inventory"/>: <c>new Inventory(items.Rules, 20)</c>.</summary>
        public IStackRules Rules { get; }

        // ── loading ──

        /// <summary>Loads the catalogs and their words (once; later calls give the same task).</summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default) => _initialization ?? (_initialization = InitializeCoreAsync(cancellationToken));

        private async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token))
            {
                var token = linked.Token;
                var addresses = await _loader.FindAddressesAsync(LoremanNames.ItemCatalogLabel, token);
                foreach (var address in addresses.OrderBy(a => a, StringComparer.Ordinal))
                {
                    var catalog = await _loader.LoadAsync<ItemCatalog>(address, token);
                    if (catalog != null) _catalogs.Add(catalog);
                }
                await _texts.InitializeAsync(_catalogs, token);
                foreach (var catalog in _catalogs) Add(catalog);
            }
            _texts.TextsChanged += OnTextsChanged;
            _ready = true;
        }

        private void Add(ItemCatalog catalog)
        {
            var data = catalog.Data;
            var folders = new ItemFolder[data.Folders.Count];
            for (var i = 0; i < folders.Length; i++)
            {
                var f = data.Folders[i];
                folders[i] = new ItemFolder(f.Id, f.Name, f.Parent >= 0 ? folders[f.Parent] : null);
                _folders.Add(folders[i]);
            }
            foreach (var item in data.Items)
            {
                if (_byId.ContainsKey(item.Id)) continue; // the same item in two builds: the first one counts
                var icon = item.Icon >= 0 ? LoremanNames.ItemIconAddress(catalog.ProjectGuid, System.IO.Path.GetFileNameWithoutExtension(data.Icons[item.Icon])) : null;
                var definition = new ItemDefinition(this, catalog, item, item.Folder >= 0 ? folders[item.Folder] : null, icon);
                _all.Add(definition);
                _byId[item.Id] = definition;
            }
        }

        private void OnTextsChanged() => TextsChanged?.Invoke();

        // ── asking ──

        public bool TryGet(Guid id, out ItemDefinition item) => _byId.TryGetValue(id, out item);

        public bool Contains(Guid id) => _byId.ContainsKey(id);

        /// <summary>The item with this id. Throws when there is none (the item was deleted in Loreman, or the build is not synced): use <see cref="TryGet"/> to ask.</summary>
        public ItemDefinition Get(Guid id)
        {
            if (_byId.TryGetValue(id, out var item)) return item;
            throw new KeyNotFoundException("Loreman: there is no item " + id + (_ready ? " in the project. Was it deleted in Loreman? Sync the build again." : ". The item database is not loaded yet: await InitializeAsync()."));
        }

        public ItemDefinition Get(ItemReference reference) => Get(reference.Guid);

        /// <summary>The item with this name in the current language (case does not matter), or null.</summary>
        public ItemDefinition Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var wanted = name.Trim();
            foreach (var item in _all)
                if (string.Equals(item.Name, wanted, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }

        /// <summary>
        /// The items that match what was typed: every word of it is a part of the name, the description or the folder, in any order. Names that begin with the first word come first.
        /// </summary>
        public IReadOnlyList<ItemDefinition> Search(string query)
        {
            var words = (query ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToArray();
            if (words.Length == 0) return _all.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            var found = new List<(ItemDefinition Item, int Score)>();
            foreach (var item in _all)
            {
                var name = item.Name.ToLowerInvariant();
                var rest = (item.Description + " " + (item.Folder?.Path ?? string.Empty)).ToLowerInvariant();
                var score = 0;
                var ok = true;
                foreach (var word in words)
                {
                    if (name.Contains(word)) score += name.StartsWith(word, StringComparison.Ordinal) ? 0 : 1;
                    else if (rest.Contains(word)) score += 3;
                    else { ok = false; break; }
                }
                if (ok) found.Add((item, score));
            }
            return found.OrderBy(f => f.Score).ThenBy(f => f.Item.Name, StringComparer.CurrentCultureIgnoreCase).Select(f => f.Item).ToList();
        }

        /// <summary>The items in a folder (“Weapons/Swords”; the path is case-insensitive). Items of the folders below are included unless <paramref name="includeSubfolders"/> is false.</summary>
        public IReadOnlyList<ItemDefinition> InFolder(string path, bool includeSubfolders = true)
        {
            var wanted = (path ?? string.Empty).Trim().Trim('/');
            var result = new List<ItemDefinition>();
            foreach (var item in _all)
            {
                var folder = item.Folder?.Path ?? string.Empty;
                var match = wanted.Length == 0
                    ? includeSubfolders || folder.Length == 0
                    : string.Equals(folder, wanted, StringComparison.OrdinalIgnoreCase) || (includeSubfolders && folder.StartsWith(wanted + "/", StringComparison.OrdinalIgnoreCase));
                if (match) result.Add(item);
            }
            return result;
        }

        // ── for ItemDefinition ──

        internal string Text(ItemCatalog catalog, TextKey key) => key.IsNone ? string.Empty : _texts.GetText(catalog.ProjectGuid, key);

        internal int MaxStackOf(ItemDefinition item) => Math.Max(1, _stackRule(item));

        internal Sprite CachedIcon(string address) => _icons.TryGetValue(address, out var sprite) ? sprite : null;

        internal Task<Sprite> LoadIconAsync(ItemDefinition item, CancellationToken cancellationToken)
        {
            var address = item.IconAddress;
            if (address == null) return Task.FromResult<Sprite>(null);
            if (_iconLoads.TryGetValue(address, out var running)) return running;
            var task = LoadIconCoreAsync(address, cancellationToken);
            _iconLoads[address] = task;
            // a load that already failed (it ran to its end before it could be remembered) is not kept: the next ask tries again
            if (task.IsCompleted && !task.IsCompletedSuccessfully) _iconLoads.Remove(address);
            return task;
        }

        private async Task<Sprite> LoadIconCoreAsync(string address, CancellationToken cancellationToken)
        {
            try
            {
                var sprite = await _loader.LoadAsync<Sprite>(address, cancellationToken);
                if (_disposed)
                {
                    if (sprite != null) _loader.Release(sprite);
                    return null;
                }
                _icons[address] = sprite;
                return sprite;
            }
            catch
            {
                _iconLoads.Remove(address); // the next ask tries again
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _texts.TextsChanged -= OnTextsChanged;
            _texts.Dispose();
            foreach (var sprite in _icons.Values)
                if (sprite != null) _loader.Release(sprite);
            _icons.Clear();
            _iconLoads.Clear();
            foreach (var catalog in _catalogs) _loader.Release(catalog);
            _catalogs.Clear();
            _all.Clear();
            _byId.Clear();
            _folders.Clear();
            _ready = false;
            _lifetime.Dispose();
        }

        private sealed class DatabaseStackRules : IStackRules
        {
            private readonly ItemDatabase _database;

            public DatabaseStackRules(ItemDatabase database)
            {
                _database = database;
            }

            public int MaxStack(Guid itemId) => _database._byId.TryGetValue(itemId, out var item) ? _database.MaxStackOf(item) : 1;
        }
    }
}
