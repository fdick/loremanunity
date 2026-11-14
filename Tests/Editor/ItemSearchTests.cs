using System.Linq;
using Loreman.Dialogues.Editor;
using NUnit.Framework;

namespace Loreman.Dialogues.Tests
{
    public class ItemSearchTests
    {
        private static readonly ItemEntry Sword = new ItemEntry("1", "Iron Sword", "Weapons/Swords", "A heavy blade.", "");
        private static readonly ItemEntry Bow = new ItemEntry("2", "Hunting Bow", "Weapons", "Light and quiet.", "");
        private static readonly ItemEntry Potion = new ItemEntry("3", "Health Potion", "Consumables", "Heals a sword wound.", "");
        private static readonly ItemEntry Key = new ItemEntry("4", "Rusty Key", "", "", "");
        private static readonly ItemEntry[] All = { Key, Potion, Bow, Sword };

        [Test]
        public void Items_are_sorted_by_folder_then_name_and_the_ones_at_the_top_level_come_last()
        {
            CollectionAssert.AreEqual(new[] { "Health Potion", "Hunting Bow", "Iron Sword", "Rusty Key" }, ItemSearch.Sort(All).Select(e => e.Name));
        }

        [Test]
        public void Every_word_must_be_in_the_name_the_folder_or_the_description()
        {
            CollectionAssert.AreEqual(new[] { "Iron Sword" }, ItemSearch.Filter(All, "iron blade").Select(e => e.Name));
            CollectionAssert.AreEqual(new[] { "Hunting Bow", "Iron Sword" }, ItemSearch.Filter(All, "weapons").Select(e => e.Name));
            Assert.AreEqual(0, ItemSearch.Filter(All, "sword banana").Count);
        }

        [Test]
        public void Names_that_begin_with_the_word_come_before_names_that_contain_it_and_those_before_the_description()
        {
            CollectionAssert.AreEqual(new[] { "Iron Sword", "Health Potion" }, ItemSearch.Filter(ItemSearch.Sort(All), "sword").Select(e => e.Name)); // the potion only mentions a sword
            var e1 = new ItemEntry("a", "Short Sword", "", "", "");
            var e2 = new ItemEntry("b", "Sword", "", "", "");
            CollectionAssert.AreEqual(new[] { "Sword", "Short Sword" }, ItemSearch.Filter(new[] { e1, e2 }, "sword").Select(e => e.Name));
        }

        [Test]
        public void An_empty_query_gives_everything_in_the_order_it_came()
        {
            var sorted = ItemSearch.Sort(All);
            CollectionAssert.AreEqual(sorted, ItemSearch.Filter(sorted, "  "));
            CollectionAssert.AreEqual(sorted, ItemSearch.Filter(sorted, null));
        }

        [Test]
        public void A_field_shows_the_name_the_folder_or_that_the_item_is_gone()
        {
            Assert.AreEqual("Weapons/Swords / Iron Sword", ItemSearch.Label(All, "1"));
            Assert.AreEqual("Rusty Key", ItemSearch.Label(All, "4"));
            Assert.AreEqual("(none)", ItemSearch.Label(All, ""));
            Assert.AreEqual("(missing) 9", ItemSearch.Label(All, "9"));
        }
    }
}
