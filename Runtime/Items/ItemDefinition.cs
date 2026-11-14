using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Loreman.Items
{
    /// <summary>A number parameter of an item (“Damage”, 15, “pt”).</summary>
    public readonly struct ItemStat
    {
        public ItemStat(string name, string unit, double value)
        {
            Name = name;
            Unit = unit;
            Value = value;
        }

        public string Name { get; }

        /// <summary>The unit, or an empty string.</summary>
        public string Unit { get; }

        public double Value { get; }

        /// <summary>“15 pt”: the number as it is shown (a whole number without a decimal point).</summary>
        public override string ToString() => (Value == Math.Floor(Value) ? ((long)Value).ToString() : Value.ToString("0.##")) + (string.IsNullOrEmpty(Unit) ? "" : " " + Unit);
    }

    /// <summary>A text parameter of an item (“Lore”: “Forged in the north.”), in the current language.</summary>
    public readonly struct ItemField
    {
        public ItemField(string name, string text)
        {
            Name = name;
            Text = text;
        }

        public string Name { get; }

        public string Text { get; }
    }

    /// <summary>A folder of the items (a category), as the game uses it.</summary>
    public sealed class ItemFolder
    {
        internal ItemFolder(Guid id, string name, ItemFolder parent)
        {
            Id = id;
            Name = name;
            Parent = parent;
            Path = parent == null ? name : parent.Path + "/" + name;
        }

        public Guid Id { get; }

        public string Name { get; }

        /// <summary>The folder it is in, or null at the top level.</summary>
        public ItemFolder Parent { get; }

        /// <summary>“Weapons/Swords”.</summary>
        public string Path { get; }

        public override string ToString() => Path;
    }

    /// <summary>
    /// An item of the game: what Loreman knows of it, in the current language. Ask the <see cref="ItemDatabase"/> for it (<c>items.Find("Iron Sword")</c>, <c>items.Get(reference)</c>).
    /// <code>
    /// item.Name; item.Description; item.GetStat("Damage"); item.GetField("Lore"); item.Folder?.Path;
    /// var icon = await item.LoadIconAsync();
    /// </code>
    /// The name and the texts always give the language the player has now; a stat is the same in every language.
    /// </summary>
    public sealed class ItemDefinition
    {
        private readonly ItemDatabase _database;
        private readonly ItemCatalog _catalog;
        private readonly ItemData _data;
        private readonly ItemStat[] _stats;

        internal ItemDefinition(ItemDatabase database, ItemCatalog catalog, ItemData data, ItemFolder folder, string iconAddress)
        {
            _database = database;
            _catalog = catalog;
            _data = data;
            Folder = folder;
            IconAddress = iconAddress;
            var items = catalog.Data;
            _stats = new ItemStat[data.Stats.Count];
            for (var i = 0; i < _stats.Length; i++)
            {
                var def = items.Stats[data.Stats[i].Stat];
                _stats[i] = new ItemStat(def.Name, def.Unit, data.Stats[i].Value);
            }
        }

        /// <summary>The id of the item in Loreman. It never changes (not when the item is renamed or moved), so a save may keep it.</summary>
        public Guid Id => _data.Id;

        /// <summary>The Loreman project the item belongs to.</summary>
        public Guid ProjectId => _catalog.ProjectGuid;

        /// <summary>The name, in the language the player has now (the language of the project where there is no translation yet).</summary>
        public string Name => _database.Text(_catalog, _data.Name);

        /// <summary>The description, or an empty string.</summary>
        public string Description => _database.Text(_catalog, _data.Description);

        /// <summary>The folder (category) the item is in, or null.</summary>
        public ItemFolder Folder { get; }

        public IReadOnlyList<ItemStat> Stats => _stats;

        public bool HasIcon => IconAddress != null;

        /// <summary>The Addressables address of the picture (null when the item has none).</summary>
        public string IconAddress { get; }

        /// <summary>The picture, once it has been loaded (<see cref="LoadIconAsync"/>); null before and for an item without one.</summary>
        public Sprite Icon => IconAddress == null ? null : _database.CachedIcon(IconAddress);

        /// <summary>How many fit in one inventory slot (see <see cref="ItemRules"/>).</summary>
        public int MaxStack => _database.MaxStackOf(this);

        public bool TryGetStat(string name, out double value)
        {
            var key = ItemRules.Normalize(name);
            foreach (var s in _stats)
            {
                if (ItemRules.Normalize(s.Name) != key) continue;
                value = s.Value;
                return true;
            }
            value = 0;
            return false;
        }

        public bool HasStat(string name) => TryGetStat(name, out _);

        /// <summary>The number parameter of this name (case does not matter), or <paramref name="fallback"/> when the item has none.</summary>
        public double GetStat(string name, double fallback = 0d) => TryGetStat(name, out var v) ? v : fallback;

        /// <summary>The text parameters with text, in the current language.</summary>
        public IEnumerable<ItemField> Fields
        {
            get
            {
                foreach (var f in _data.Fields) yield return new ItemField(f.Name, _database.Text(_catalog, f.Text));
            }
        }

        public bool HasField(string name)
        {
            var key = ItemRules.Normalize(name);
            foreach (var f in _data.Fields)
                if (ItemRules.Normalize(f.Name) == key) return true;
            return false;
        }

        /// <summary>The text parameter of this name, or an empty string.</summary>
        public string GetField(string name)
        {
            var key = ItemRules.Normalize(name);
            foreach (var f in _data.Fields)
                if (ItemRules.Normalize(f.Name) == key) return _database.Text(_catalog, f.Text);
            return string.Empty;
        }

        /// <summary>Loads the picture (once: the next calls give the same one). Null for an item that has none. The database lets it go when it is disposed.</summary>
        public Task<Sprite> LoadIconAsync(CancellationToken cancellationToken = default) => _database.LoadIconAsync(this, cancellationToken);

        public override string ToString() => Name;
    }
}
