using System;
using System.Collections.Generic;
using Loreman.Dialogues;

namespace Loreman.Items
{
    /// <summary>A number parameter that some items have: its name and unit (“Damage”, “pt”). Items refer to it by its index in <see cref="ItemsData.Stats"/>.</summary>
    public readonly struct ItemStatDefinition
    {
        public ItemStatDefinition(string name, string unit)
        {
            Name = name;
            Unit = unit;
        }

        public string Name { get; }

        /// <summary>The unit (“kg”, “%”), or an empty string.</summary>
        public string Unit { get; }
    }

    /// <summary>A folder of the Items tool (a category: Weapons, Potions). Folders nest.</summary>
    public sealed class ItemFolderData
    {
        public ItemFolderData(Guid id, int parent, string name)
        {
            Id = id;
            Parent = parent;
            Name = name;
        }

        public Guid Id { get; }

        /// <summary>The index of the parent folder in <see cref="ItemsData.Folders"/>, or -1 at the top level.</summary>
        public int Parent { get; }

        public string Name { get; }
    }

    /// <summary>The value of a number parameter of one item.</summary>
    public readonly struct ItemStatValue
    {
        public ItemStatValue(int stat, double value)
        {
            Stat = stat;
            Value = value;
        }

        /// <summary>The index in <see cref="ItemsData.Stats"/>.</summary>
        public int Stat { get; }

        public double Value { get; }
    }

    /// <summary>A text parameter of one item (a lore entry, a letter): its name and where its words are in the items table.</summary>
    public readonly struct ItemFieldValue
    {
        public ItemFieldValue(string name, TextKey text)
        {
            Name = name;
            Text = text;
        }

        public string Name { get; }

        public TextKey Text { get; }
    }

    /// <summary>One item, as the build holds it. The words are not here, only their keys (<see cref="Name"/>, <see cref="Description"/>, the fields).</summary>
    public sealed class ItemData
    {
        private static readonly IReadOnlyList<ItemStatValue> NoStats = Array.Empty<ItemStatValue>();
        private static readonly IReadOnlyList<ItemFieldValue> NoFields = Array.Empty<ItemFieldValue>();

        public ItemData(
            Guid id, int folder, TextKey name, TextKey description, int icon, IReadOnlyList<ItemStatValue> stats, IReadOnlyList<ItemFieldValue> fields)
        {
            Id = id;
            Folder = folder;
            Name = name;
            Description = description;
            Icon = icon;
            Stats = stats ?? NoStats;
            Fields = fields ?? NoFields;
        }

        /// <summary>The id of the item in Loreman. It never changes (not when the item is renamed or moved), so a save may keep it.</summary>
        public Guid Id { get; }

        /// <summary>The index in <see cref="ItemsData.Folders"/>, or -1 at the top level.</summary>
        public int Folder { get; }

        public TextKey Name { get; }

        /// <summary>None when the item has no description.</summary>
        public TextKey Description { get; }

        /// <summary>The index in <see cref="ItemsData.Icons"/>, or -1 when the item has no picture.</summary>
        public int Icon { get; }

        public IReadOnlyList<ItemStatValue> Stats { get; }

        public IReadOnlyList<ItemFieldValue> Fields { get; }
    }

    /// <summary>Everything the items of one build are: read from <c>items/items.bin</c> (see <see cref="ItemsReader"/>).</summary>
    public sealed class ItemsData
    {
        public ItemsData(
            IReadOnlyList<ItemStatDefinition> stats, IReadOnlyList<ItemFolderData> folders, IReadOnlyList<string> icons, IReadOnlyList<ItemData> items)
        {
            Stats = stats;
            Folders = folders;
            Icons = icons;
            Items = items;
        }

        /// <summary>The number parameters in use, each once.</summary>
        public IReadOnlyList<ItemStatDefinition> Stats { get; }

        /// <summary>A parent comes before its children.</summary>
        public IReadOnlyList<ItemFolderData> Folders { get; }

        /// <summary>The paths of the pictures in the build (<c>items/icons/{code}.png</c>); items that share a picture share an entry.</summary>
        public IReadOnlyList<string> Icons { get; }

        public IReadOnlyList<ItemData> Items { get; }
    }
}
