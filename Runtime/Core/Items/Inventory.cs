using System;
using System.Collections.Generic;

namespace Loreman.Items
{
    /// <summary>Some of one item in one slot.</summary>
    public readonly struct ItemStack : IEquatable<ItemStack>
    {
        public static readonly ItemStack Empty = default;

        public ItemStack(Guid itemId, int count)
        {
            ItemId = count > 0 ? itemId : Guid.Empty;
            Count = count > 0 ? count : 0;
        }

        public Guid ItemId { get; }

        public int Count { get; }

        public bool IsEmpty => Count <= 0;

        public bool Equals(ItemStack other) => ItemId == other.ItemId && Count == other.Count;
        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);
        public override int GetHashCode() => ItemId.GetHashCode() * 397 ^ Count;
        public override string ToString() => IsEmpty ? "(empty)" : ItemId + " × " + Count;
    }

    /// <summary>How many of an item fit in one slot. The inventory asks; where the answer comes from (a number parameter of the item, a table) is not its business.</summary>
    public interface IStackRules
    {
        /// <summary>At least 1. An item that does not stack answers 1.</summary>
        int MaxStack(Guid itemId);
    }

    /// <summary>The same limit for every item (a test, a game where everything stacks to 99).</summary>
    public sealed class FixedStackRules : IStackRules
    {
        private readonly int _max;

        public FixedStackRules(int max)
        {
            _max = Math.Max(1, max);
        }

        public int MaxStack(Guid itemId) => _max;
    }

    public enum InventoryChangeKind
    {
        /// <summary>Items came in (<see cref="Inventory.Add"/>).</summary>
        Added,

        /// <summary>Items went out (<see cref="Inventory.Remove"/>, <see cref="Inventory.RemoveAt"/>).</summary>
        Removed,

        /// <summary>Items changed places (<see cref="Inventory.Move"/>, <see cref="Inventory.Split"/>, <see cref="Inventory.Sort"/>): nothing came or went.</summary>
        Rearranged,

        /// <summary>Everything went (<see cref="Inventory.Clear"/>) or everything was replaced (<see cref="Inventory.Load"/>).</summary>
        Reset,
    }

    /// <summary>What one operation did, for a screen that shows the inventory (it may simply draw everything again).</summary>
    public readonly struct InventoryChange
    {
        public InventoryChange(InventoryChangeKind kind, Guid itemId, int count)
        {
            Kind = kind;
            ItemId = itemId;
            Count = count;
        }

        public InventoryChangeKind Kind { get; }

        /// <summary>The item that came or went (empty for a rearrangement and a reset).</summary>
        public Guid ItemId { get; }

        /// <summary>How many came or went (0 for a rearrangement and a reset).</summary>
        public int Count { get; }
    }

    /// <summary>The inventory as it is saved: what lies in which slot. Plain fields, so JsonUtility, Newtonsoft or anything else writes it.</summary>
    [Serializable]
    public sealed class InventorySave
    {
        /// <summary>0 for an inventory without a limit.</summary>
        public int slots;

        public List<SavedStack> stacks = new List<SavedStack>();
    }

    [Serializable]
    public sealed class SavedStack
    {
        public int slot;

        /// <summary>The id of the item in Loreman (<c>Guid.ToString("D")</c>).</summary>
        public string item;

        public int count;
    }

    /// <summary>
    /// A place that holds items: a bag, a chest, a shop's shelf, an equipment panel. It knows nothing about Unity or about what an item is (only its id and how many fit in a slot),
    /// so it is the same for a player, a chest and a test.
    /// <list type="bullet">
    /// <item>With a number of slots (<c>new Inventory(rules, 20)</c>) it is a grid: an empty slot stays where it is, so a screen can show each slot at its place.</item>
    /// <item>Without (<c>slots: 0</c>) it is a list that grows: there are no empty slots, and a slot that empties goes.</item>
    /// </list>
    /// Everything either happens or does not: <see cref="TryAdd"/> and <see cref="TryRemove"/> change nothing when the whole amount cannot be done.
    /// </summary>
    public sealed class Inventory
    {
        private readonly IStackRules _rules;
        private readonly List<ItemStack> _slots = new List<ItemStack>();
        private readonly int _capacity;

        /// <param name="slots">The number of slots; 0 for no limit.</param>
        public Inventory(IStackRules rules, int slots = 0)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (slots < 0) throw new ArgumentOutOfRangeException(nameof(slots));
            _capacity = slots;
            for (var i = 0; i < slots; i++) _slots.Add(ItemStack.Empty);
        }

        /// <summary>Something changed (once per operation). Not raised while nothing changed (an <see cref="Add"/> that found no room).</summary>
        public event Action<InventoryChange> Changed;

        public bool IsUnlimited => _capacity == 0;

        /// <summary>The number of slots (for an unlimited inventory: the number of stacks there are now).</summary>
        public int SlotCount => _slots.Count;

        public IReadOnlyList<ItemStack> Slots => _slots;

        public ItemStack this[int slot] => _slots[slot];

        /// <summary>The number of slots that hold something.</summary>
        public int UsedSlots
        {
            get
            {
                var used = 0;
                foreach (var s in _slots)
                    if (!s.IsEmpty) used++;
                return used;
            }
        }

        public int MaxStack(Guid itemId) => Math.Max(1, _rules.MaxStack(itemId));

        // ── asking ──

        /// <summary>How many of the item there are in all slots.</summary>
        public int CountOf(Guid itemId)
        {
            var total = 0;
            foreach (var s in _slots)
                if (s.ItemId == itemId) total += s.Count;
            return total;
        }

        public bool Has(Guid itemId, int count = 1) => count <= 0 || CountOf(itemId) >= count;

        /// <summary>How many more of the item would fit now (fills the stacks that have room, then the empty slots). <see cref="int.MaxValue"/> for an unlimited inventory.</summary>
        public int RoomFor(Guid itemId)
        {
            if (IsUnlimited) return int.MaxValue;
            var max = MaxStack(itemId);
            long room = 0;
            foreach (var s in _slots)
            {
                if (s.IsEmpty) room += max;
                else if (s.ItemId == itemId) room += Math.Max(0, max - s.Count);
            }
            return (int)Math.Min(room, int.MaxValue);
        }

        public bool CanAdd(Guid itemId, int count = 1) => count <= 0 || RoomFor(itemId) >= count;

        /// <summary>The slot of the first stack of the item, or -1.</summary>
        public int FindSlot(Guid itemId)
        {
            for (var i = 0; i < _slots.Count; i++)
                if (_slots[i].ItemId == itemId && !_slots[i].IsEmpty) return i;
            return -1;
        }

        // ── putting in ──

        /// <summary>Puts items in: onto the stacks of the same item that have room first, then into empty slots. Returns how many did <b>not</b> fit (0: all went in).</summary>
        public int Add(Guid itemId, int count = 1)
        {
            if (itemId == Guid.Empty) throw new ArgumentException("An item has an id.", nameof(itemId));
            if (count <= 0) return 0;
            var max = MaxStack(itemId);
            var left = count;

            for (var i = 0; i < _slots.Count && left > 0; i++)
            {
                var s = _slots[i];
                if (s.ItemId != itemId || s.IsEmpty || s.Count >= max) continue;
                var put = Math.Min(left, max - s.Count);
                _slots[i] = new ItemStack(itemId, s.Count + put);
                left -= put;
            }
            for (var i = 0; i < _slots.Count && left > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                var put = Math.Min(left, max);
                _slots[i] = new ItemStack(itemId, put);
                left -= put;
            }
            while (IsUnlimited && left > 0)
            {
                var put = Math.Min(left, max);
                _slots.Add(new ItemStack(itemId, put));
                left -= put;
            }

            if (left < count) Changed?.Invoke(new InventoryChange(InventoryChangeKind.Added, itemId, count - left));
            return left;
        }

        /// <summary>Puts all of them in, or none: false (and nothing changed) when they do not all fit.</summary>
        public bool TryAdd(Guid itemId, int count = 1)
        {
            if (count <= 0) return true;
            if (!CanAdd(itemId, count)) return false;
            Add(itemId, count);
            return true;
        }

        // ── taking out ──

        /// <summary>Takes items out, starting with the last slot (so the stacks at the front of the bag stay full). Returns how many were taken (less than asked when there were not enough).</summary>
        public int Remove(Guid itemId, int count = 1)
        {
            if (count <= 0) return 0;
            var left = count;
            for (var i = _slots.Count - 1; i >= 0 && left > 0; i--)
            {
                var s = _slots[i];
                if (s.ItemId != itemId || s.IsEmpty) continue;
                var take = Math.Min(left, s.Count);
                Set(i, s.Count - take == 0 ? ItemStack.Empty : new ItemStack(itemId, s.Count - take));
                left -= take;
            }
            var taken = count - left;
            if (taken > 0)
            {
                Tidy();
                Changed?.Invoke(new InventoryChange(InventoryChangeKind.Removed, itemId, taken));
            }
            return taken;
        }

        /// <summary>Takes all of them or none: false (and nothing changed) when there are not enough.</summary>
        public bool TryRemove(Guid itemId, int count = 1)
        {
            if (count <= 0) return true;
            if (CountOf(itemId) < count) return false;
            Remove(itemId, count);
            return true;
        }

        /// <summary>Takes up to <paramref name="count"/> out of one slot. Returns the stack that was taken (empty when the slot was empty).</summary>
        public ItemStack RemoveAt(int slot, int count = int.MaxValue)
        {
            var s = _slots[slot];
            if (s.IsEmpty || count <= 0) return ItemStack.Empty;
            var take = Math.Min(count, s.Count);
            Set(slot, take == s.Count ? ItemStack.Empty : new ItemStack(s.ItemId, s.Count - take));
            var taken = new ItemStack(s.ItemId, take);
            Tidy();
            Changed?.Invoke(new InventoryChange(InventoryChangeKind.Removed, s.ItemId, take));
            return taken;
        }

        // ── arranging ──

        /// <summary>
        /// Moves what is in a slot to another. The same item goes onto the stack up to its limit (what does not fit stays); a different item, or an empty slot, swaps.
        /// Returns false when nothing moved (the same slot, an empty source, or a full stack of the same item).
        /// </summary>
        public bool Move(int from, int to)
        {
            if (from == to) return false;
            var a = _slots[from];
            var b = _slots[to];
            if (a.IsEmpty) return false;

            if (!b.IsEmpty && b.ItemId == a.ItemId)
            {
                var room = MaxStack(a.ItemId) - b.Count;
                if (room <= 0) return false;
                var put = Math.Min(room, a.Count);
                _slots[to] = new ItemStack(a.ItemId, b.Count + put);
                Set(from, put == a.Count ? ItemStack.Empty : new ItemStack(a.ItemId, a.Count - put));
            }
            else
            {
                _slots[from] = b;
                _slots[to] = a;
            }
            Tidy();
            Changed?.Invoke(new InventoryChange(InventoryChangeKind.Rearranged, Guid.Empty, 0));
            return true;
        }

        /// <summary>Takes <paramref name="count"/> out of a stack and puts them into an empty slot (the first one when <paramref name="to"/> is -1). Returns the slot, or -1 when it could not be done.</summary>
        public int Split(int slot, int count, int to = -1)
        {
            var s = _slots[slot];
            if (s.IsEmpty || count <= 0 || count >= s.Count) return -1;
            if (to < 0)
            {
                for (var i = 0; i < _slots.Count; i++)
                    if (_slots[i].IsEmpty) { to = i; break; }
                if (to < 0 && IsUnlimited)
                {
                    _slots.Add(ItemStack.Empty);
                    to = _slots.Count - 1;
                }
            }
            if (to < 0 || to == slot || !_slots[to].IsEmpty) return -1;
            _slots[slot] = new ItemStack(s.ItemId, s.Count - count);
            _slots[to] = new ItemStack(s.ItemId, count);
            Changed?.Invoke(new InventoryChange(InventoryChangeKind.Rearranged, Guid.Empty, 0));
            return to;
        }

        /// <summary>Merges the stacks of every item and puts them in the order the comparer gives (an order of item ids is the default); empty slots go to the end.</summary>
        public void Sort(IComparer<Guid> order = null)
        {
            var totals = new Dictionary<Guid, int>();
            var ids = new List<Guid>();
            foreach (var s in _slots)
            {
                if (s.IsEmpty) continue;
                if (!totals.ContainsKey(s.ItemId)) ids.Add(s.ItemId);
                totals[s.ItemId] = (totals.TryGetValue(s.ItemId, out var have) ? have : 0) + s.Count;
            }
            ids.Sort(order ?? Comparer<Guid>.Default);

            var sorted = new List<ItemStack>();
            foreach (var id in ids)
            {
                var max = MaxStack(id);
                for (var left = totals[id]; left > 0;)
                {
                    var put = Math.Min(left, max);
                    sorted.Add(new ItemStack(id, put));
                    left -= put;
                }
            }
            if (!IsUnlimited) while (sorted.Count < _capacity) sorted.Add(ItemStack.Empty);

            var same = sorted.Count == _slots.Count;
            for (var i = 0; same && i < sorted.Count; i++) same = sorted[i].Equals(_slots[i]);
            if (same) return;
            _slots.Clear();
            _slots.AddRange(sorted);
            Changed?.Invoke(new InventoryChange(InventoryChangeKind.Rearranged, Guid.Empty, 0));
        }

        /// <summary>Moves everything into another inventory as far as it fits; what does not fit stays here. Returns how many items moved.</summary>
        public int TransferTo(Inventory other, Guid itemId, int count = int.MaxValue)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (ReferenceEquals(other, this)) return 0;
            var amount = Math.Min(count, CountOf(itemId));
            if (amount <= 0) return 0;
            var room = other.RoomFor(itemId);
            amount = Math.Min(amount, room);
            if (amount <= 0) return 0;
            Remove(itemId, amount);
            other.Add(itemId, amount);
            return amount;
        }

        public void Clear()
        {
            var had = false;
            foreach (var s in _slots) had |= !s.IsEmpty;
            _slots.Clear();
            for (var i = 0; i < _capacity; i++) _slots.Add(ItemStack.Empty);
            if (had) Changed?.Invoke(new InventoryChange(InventoryChangeKind.Reset, Guid.Empty, 0));
        }

        // ── saving ──

        public InventorySave Save()
        {
            var save = new InventorySave { slots = _capacity };
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (!s.IsEmpty) save.stacks.Add(new SavedStack { slot = i, item = s.ItemId.ToString("D"), count = s.Count });
            }
            return save;
        }

        /// <summary>
        /// Replaces the contents with a saved one. A stack of an item that <paramref name="isKnown"/> refuses (the item was deleted in Loreman since) is dropped, and so is what no
        /// longer fits (a smaller inventory than it was saved from). Returns how many items were lost that way.
        /// </summary>
        public int Load(InventorySave save, Func<Guid, bool> isKnown = null)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            _slots.Clear();
            for (var i = 0; i < _capacity; i++) _slots.Add(ItemStack.Empty);

            var lost = 0;
            var overflow = new List<ItemStack>();
            foreach (var stack in save.stacks)
            {
                if (stack == null || stack.count <= 0 || !Guid.TryParse(stack.item, out var id) || id == Guid.Empty) continue;
                if (isKnown != null && !isKnown(id)) { lost += stack.count; continue; }
                var count = Math.Min(stack.count, MaxStack(id));
                lost += stack.count - count; // a stack bigger than the limit is cut to it (the limit was lowered in Loreman)
                if (!IsUnlimited && stack.slot >= 0 && stack.slot < _capacity && _slots[stack.slot].IsEmpty) _slots[stack.slot] = new ItemStack(id, count);
                else overflow.Add(new ItemStack(id, count));
            }
            if (IsUnlimited)
            {
                // an unlimited list has no places to keep: stacks follow in the order they were saved
                foreach (var o in overflow) _slots.Add(o);
            }
            else
            {
                // a stack whose slot does not exist or was taken goes into the first free slot; what finds none is lost
                foreach (var o in overflow)
                {
                    var placed = false;
                    for (var i = 0; i < _capacity && !placed; i++)
                    {
                        if (!_slots[i].IsEmpty) continue;
                        _slots[i] = o;
                        placed = true;
                    }
                    if (!placed) lost += o.Count;
                }
            }
            Changed?.Invoke(new InventoryChange(InventoryChangeKind.Reset, Guid.Empty, 0));
            return lost;
        }

        // ── inside ──

        private void Set(int slot, ItemStack stack) => _slots[slot] = stack;

        /// <summary>An unlimited inventory has no empty slots.</summary>
        private void Tidy()
        {
            if (IsUnlimited) _slots.RemoveAll(s => s.IsEmpty);
        }
    }
}
