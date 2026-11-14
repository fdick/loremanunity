using System;
using UnityEngine;

namespace Loreman.Items
{
    /// <summary>
    /// An item picked in the inspector: use it as a field of a component and choose the item from a list with a search box and pictures (the list is what the importer made).
    /// It keeps only the id of the item in Loreman, which does not change when the item is renamed or moved.
    /// <code>[SerializeField] private ItemReference key;      // then: items.Get(key)</code>
    /// </summary>
    [Serializable]
    public struct ItemReference
    {
        [SerializeField] private string id;

        public ItemReference(Guid itemId)
        {
            id = itemId == Guid.Empty ? string.Empty : itemId.ToString("D");
        }

        public ItemReference(ItemDefinition item) : this(item == null ? Guid.Empty : item.Id)
        {
        }

        public bool IsSet => Guid != Guid.Empty;

        public Guid Guid => System.Guid.TryParse(id, out var guid) ? guid : System.Guid.Empty;

        public static implicit operator Guid(ItemReference reference) => reference.Guid;

        public static implicit operator ItemReference(ItemDefinition item) => new ItemReference(item);

        public override string ToString() => IsSet ? id : "(no item)";
    }
}
