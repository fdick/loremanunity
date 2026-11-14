using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Loreman.Items
{
    /// <summary>One line of what an inventory starts with.</summary>
    [Serializable]
    public struct StartingItem
    {
        public ItemReference item;

        [Min(1)]
        public int count;
    }

    /// <summary>
    /// An inventory on a GameObject: a player's bag, a chest, a shop. Put it on the object, set the number of slots, pick what it starts with, and use it:
    /// <code>
    /// [SerializeField] private InventoryComponent bag;
    /// [SerializeField] private ItemReference potion;
    /// bag.Add(potion, 3);                       // 3 potions; the leftover is returned when they do not all fit
    /// if (bag.Has("Rusty Key")) OpenDoor();
    /// bag.Changed += change => RefreshScreen();
    /// PlayerPrefs.SetString("bag", bag.SaveToJson());     // and bag.LoadFromJson(...)
    /// </code>
    /// It gets its items from the shared database (<see cref="LoremanItems"/>) when it starts; use it after <see cref="Ready"/> (or <c>await bag.InitializeAsync()</c>).
    /// The logic itself is <see cref="Inventory"/>, which has nothing to do with Unity and can be used without this component.
    /// </summary>
    [AddComponentMenu("Loreman/Inventory")]
    public sealed class InventoryComponent : MonoBehaviour
    {
        [Min(0)]
        [Tooltip("The number of slots. 0: no limit, a list that grows.")]
        [SerializeField] private int slots = 20;

        [Tooltip("What the inventory holds at the start (when it is not loaded from a save).")]
        [SerializeField] private List<StartingItem> startingItems = new List<StartingItem>();

        [Tooltip("Raised after anything came in, went out or moved. A screen may simply draw the inventory again.")]
        [SerializeField] private UnityEvent changed = new UnityEvent();

        private ItemDatabase _database;
        private Inventory _inventory;
        private Task _initialization;

        /// <summary>The items are loaded and the inventory works.</summary>
        public event Action Ready;

        /// <summary>Anything came in, went out or moved (see <see cref="InventoryChange"/>).</summary>
        public event Action<InventoryChange> Changed;

        public bool IsReady => _inventory != null;

        public int SlotCount => slots;

        /// <summary>The items of the game this inventory knows (null until it is ready).</summary>
        public ItemDatabase Database => _database;

        /// <summary>The inventory itself: slots, stacks, moving. Only after <see cref="IsReady"/>.</summary>
        public Inventory Inventory => _inventory ?? throw NotReady();

        /// <summary>Uses a database of your own (instead of the shared one). Call it before the component starts.</summary>
        public void UseDatabase(ItemDatabase database)
        {
            if (_initialization != null) throw new InvalidOperationException("Loreman: UseDatabase must be called before the inventory starts.");
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Waits for the items and makes the inventory (once). Called by the component itself.</summary>
        public Task InitializeAsync() => _initialization ?? (_initialization = InitializeCoreAsync());

        private async Task InitializeCoreAsync()
        {
            if (_database == null) _database = await LoremanItems.GetAsync();
            else await _database.InitializeAsync();
            if (this == null) return; // destroyed while loading

            _inventory = new Inventory(_database.Rules, slots);
            foreach (var s in startingItems)
            {
                if (!s.item.IsSet || !_database.TryGet(s.item.Guid, out var item))
                {
                    if (s.item.IsSet) Debug.LogWarning("Loreman: the starting item " + s.item + " of " + name + " is not in the project (deleted in Loreman?).", this);
                    continue;
                }
                _inventory.Add(item.Id, Mathf.Max(1, s.count));
            }
            _inventory.Changed += OnChanged;
            Ready?.Invoke();
            OnChanged(new InventoryChange(InventoryChangeKind.Reset, Guid.Empty, 0));
        }

        private void OnChanged(InventoryChange change)
        {
            Changed?.Invoke(change);
            changed.Invoke();
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.Changed -= OnChanged;
        }

        private InvalidOperationException NotReady() =>
            new InvalidOperationException("Loreman: the inventory of " + name + " is not ready yet. Wait for the Ready event, or await InitializeAsync().");

        // ── asking: by item, by reference, by name ──

        /// <summary>The item in a slot, or null for an empty one.</summary>
        public ItemDefinition ItemAt(int slot)
        {
            var stack = Inventory[slot];
            return stack.IsEmpty ? null : _database.Get(stack.ItemId);
        }

        public int CountOf(ItemDefinition item) => Inventory.CountOf(item.Id);

        public int CountOf(ItemReference item) => Inventory.CountOf(item.Guid);

        public int CountOf(string itemName) => Inventory.CountOf(ByName(itemName).Id);

        public bool Has(ItemDefinition item, int count = 1) => Inventory.Has(item.Id, count);

        public bool Has(ItemReference item, int count = 1) => Inventory.Has(item.Guid, count);

        /// <summary>Has this many of the item with this name in the current language (false when no item has the name).</summary>
        public bool Has(string itemName, int count = 1)
        {
            var item = _database?.Find(itemName);
            return item != null && Inventory.Has(item.Id, count);
        }

        public bool CanAdd(ItemDefinition item, int count = 1) => Inventory.CanAdd(item.Id, count);

        // ── putting in and taking out ──

        /// <summary>Puts items in. Returns how many did <b>not</b> fit (0: all went in).</summary>
        public int Add(ItemDefinition item, int count = 1) => Inventory.Add((item ?? throw new ArgumentNullException(nameof(item))).Id, count);

        public int Add(ItemReference item, int count = 1) => Add(_database.Get(item), count);

        public int Add(string itemName, int count = 1) => Add(ByName(itemName), count);

        /// <summary>Puts all in, or none (false when they do not all fit).</summary>
        public bool TryAdd(ItemDefinition item, int count = 1) => Inventory.TryAdd(item.Id, count);

        public bool TryAdd(ItemReference item, int count = 1) => TryAdd(_database.Get(item), count);

        public bool TryAdd(string itemName, int count = 1) => TryAdd(ByName(itemName), count);

        /// <summary>Takes items out. Returns how many were taken.</summary>
        public int Remove(ItemDefinition item, int count = 1) => Inventory.Remove(item.Id, count);

        public int Remove(ItemReference item, int count = 1) => Inventory.Remove(item.Guid, count);

        public int Remove(string itemName, int count = 1) => Remove(ByName(itemName), count);

        /// <summary>Takes all out, or none (false when there are not enough).</summary>
        public bool TryRemove(ItemDefinition item, int count = 1) => Inventory.TryRemove(item.Id, count);

        public bool TryRemove(ItemReference item, int count = 1) => Inventory.TryRemove(item.Guid, count);

        public bool TryRemove(string itemName, int count = 1) => TryRemove(ByName(itemName), count);

        /// <summary>Moves everything of an item to another inventory as far as it fits. Returns how many moved.</summary>
        public int Give(InventoryComponent other, ItemDefinition item, int count = int.MaxValue) => Inventory.TransferTo(other.Inventory, item.Id, count);

        private ItemDefinition ByName(string itemName) =>
            (_database ?? throw NotReady()).Find(itemName) ?? throw new KeyNotFoundException("Loreman: no item is called \"" + itemName + "\" (in the current language).");

        // ── saving ──

        /// <summary>The contents as JSON, to keep in a save (an item is kept by its id, which does not change when it is renamed).</summary>
        public string SaveToJson() => JsonUtility.ToJson(Inventory.Save());

        /// <summary>Replaces the contents with a saved one. What the project no longer has (an item deleted in Loreman since) is dropped; the number of items lost is returned.</summary>
        public int LoadFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("There is nothing to load.", nameof(json));
            var save = JsonUtility.FromJson<InventorySave>(json) ?? throw new ArgumentException("This is not a saved inventory.", nameof(json));
            var lost = Inventory.Load(save, _database.Contains);
            if (lost > 0) Debug.LogWarning("Loreman: " + lost + " item(s) of the saved inventory are not in the project any more and were dropped.", this);
            return lost;
        }
    }
}
