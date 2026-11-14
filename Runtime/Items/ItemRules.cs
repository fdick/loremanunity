using System;
using System.Linq;

namespace Loreman.Items
{
    /// <summary>
    /// What the package takes from the parameters a designer gave an item. Loreman has no “stack size” field, so a number parameter does it:
    /// give an item a number parameter called <b>Max Stack</b> (also <c>Stack</c>, <c>Stack Size</c>, <c>Max Stack Size</c>; case and spaces do not matter) and that many fit in one slot.
    /// An item without it does not stack (one to a slot).
    /// </summary>
    public static class ItemRules
    {
        private static readonly string[] StackNames = { "maxstack", "stack", "stacksize", "maxstacksize" };

        /// <summary>A name as it is compared: lower case, no spaces, so “Max Stack”, “max stack” and “MaxStack” are one.</summary>
        public static string Normalize(string name) => string.IsNullOrEmpty(name) ? string.Empty : new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

        /// <summary>The number of an item that fit in a slot: its Max Stack parameter, 1 when it has none.</summary>
        public static int StackOf(ItemDefinition item)
        {
            if (item == null) return 1;
            foreach (var s in item.Stats)
            {
                if (Array.IndexOf(StackNames, Normalize(s.Name)) < 0) continue;
                return (int)Math.Max(1d, Math.Min(Math.Round(s.Value), int.MaxValue));
            }
            return 1;
        }
    }
}
