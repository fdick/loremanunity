using System;
using System.Collections.Generic;
using System.Linq;
using Loreman.Items;
using NUnit.Framework;

namespace Loreman.Dialogues.Tests
{
    public class InventoryTests
    {
        private static readonly Guid Sword = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid Potion = Guid.Parse("00000000-0000-0000-0000-000000000002");
        private static readonly Guid Coin = Guid.Parse("00000000-0000-0000-0000-000000000003");

        private sealed class Rules : IStackRules
        {
            public int MaxStack(Guid itemId) => itemId == Sword ? 1 : itemId == Potion ? 5 : 100;
        }

        private static Inventory Bag(int slots = 4) => new Inventory(new Rules(), slots);

        private static string Show(Inventory bag) => string.Join(" ", bag.Slots.Select(s => s.IsEmpty ? "-" : (s.ItemId == Sword ? "S" : s.ItemId == Potion ? "P" : "C") + s.Count));

        // ── putting in ──

        [Test]
        public void Items_fill_the_stack_that_has_room_and_then_the_next_empty_slot()
        {
            var bag = Bag();
            Assert.AreEqual(0, bag.Add(Potion, 3));
            Assert.AreEqual(0, bag.Add(Potion, 4)); // 5 + 2
            Assert.AreEqual("P5 P2 - -", Show(bag));
            Assert.AreEqual(7, bag.CountOf(Potion));
        }

        [Test]
        public void An_item_that_does_not_stack_takes_a_slot_each_and_what_does_not_fit_is_returned()
        {
            var bag = Bag(2);
            Assert.AreEqual(1, bag.Add(Sword, 3));
            Assert.AreEqual("S1 S1", Show(bag));
            Assert.AreEqual(2, bag.CountOf(Sword));
        }

        [Test]
        public void TryAdd_puts_all_in_or_nothing()
        {
            var bag = Bag(1);
            Assert.IsFalse(bag.TryAdd(Potion, 6));
            Assert.AreEqual("-", Show(bag));
            Assert.IsTrue(bag.TryAdd(Potion, 5));
            Assert.AreEqual("P5", Show(bag));
            Assert.IsFalse(bag.CanAdd(Potion, 1));
            Assert.AreEqual(0, bag.RoomFor(Potion));
        }

        [Test]
        public void An_unlimited_inventory_grows_and_has_no_empty_slots()
        {
            var bag = Bag(0);
            Assert.IsTrue(bag.IsUnlimited);
            Assert.AreEqual(0, bag.Add(Potion, 12));
            Assert.AreEqual("P5 P5 P2", Show(bag));
            bag.Remove(Potion, 12);
            Assert.AreEqual(0, bag.SlotCount);
            Assert.AreEqual(int.MaxValue, bag.RoomFor(Potion));
        }

        [Test]
        public void An_empty_id_and_a_non_positive_count_are_handled()
        {
            var bag = Bag();
            Assert.Throws<ArgumentException>(() => bag.Add(Guid.Empty));
            Assert.AreEqual(0, bag.Add(Potion, 0));
            Assert.AreEqual(0, bag.Add(Potion, -3));
            Assert.AreEqual("- - - -", Show(bag));
        }

        // ── taking out ──

        [Test]
        public void Items_are_taken_from_the_last_stack_first()
        {
            var bag = Bag();
            bag.Add(Potion, 8); // P5 P3
            Assert.AreEqual(3, bag.Remove(Potion, 3));
            Assert.AreEqual("P5 - - -", Show(bag), "the last stack went first and the one at the front stays full");
            Assert.AreEqual(2, bag.Remove(Potion, 2));
            Assert.AreEqual("P3 - - -", Show(bag));
        }

        [Test]
        public void Remove_gives_what_there_was_and_TryRemove_all_or_nothing()
        {
            var bag = Bag();
            bag.Add(Coin, 10);
            Assert.AreEqual(10, bag.Remove(Coin, 50));
            bag.Add(Coin, 10);
            Assert.IsFalse(bag.TryRemove(Coin, 11));
            Assert.AreEqual(10, bag.CountOf(Coin));
            Assert.IsTrue(bag.TryRemove(Coin, 10));
            Assert.IsFalse(bag.Has(Coin));
            Assert.IsTrue(bag.Has(Coin, 0));
        }

        [Test]
        public void RemoveAt_takes_from_one_slot()
        {
            var bag = Bag();
            bag.Add(Potion, 8);
            var taken = bag.RemoveAt(0, 2);
            Assert.AreEqual(new ItemStack(Potion, 2), taken);
            Assert.AreEqual("P3 P3 - -", Show(bag));
            Assert.AreEqual(ItemStack.Empty, bag.RemoveAt(3));
        }

        // ── arranging ──

        [Test]
        public void Moving_onto_the_same_item_merges_up_to_the_limit_and_the_rest_stays()
        {
            var bag = Bag();
            bag.Add(Potion, 8); // P5 P3
            Assert.IsTrue(bag.Move(0, 1));
            Assert.AreEqual("P3 P5 - -", Show(bag), "two of the first stack filled the second; three stay");
            Assert.IsFalse(bag.Move(0, 1), "the stack it would go onto is full: nothing moves");
            Assert.IsFalse(bag.Move(2, 0), "nothing to move");
            Assert.IsFalse(bag.Move(1, 1), "the same slot");
        }

        [Test]
        public void Moving_onto_an_empty_slot_or_another_item_swaps()
        {
            var bag = Bag();
            bag.Add(Potion, 3);
            bag.Add(Sword);
            bag.Add(Potion, 4);
            Assert.AreEqual("P5 S1 P2 -", Show(bag));
            Assert.IsTrue(bag.Move(0, 3));
            Assert.AreEqual("- S1 P2 P5", Show(bag));
            Assert.IsTrue(bag.Move(1, 2));
            Assert.AreEqual("- P2 S1 P5", Show(bag));
            bag.Remove(Potion, 3); // from the last stack: P5 → P2
            Assert.AreEqual("- P2 S1 P2", Show(bag));
            Assert.IsTrue(bag.Move(3, 1));
            Assert.AreEqual("- P4 S1 -", Show(bag));
        }

        [Test]
        public void Split_takes_some_out_of_a_stack_into_an_empty_slot()
        {
            var bag = Bag();
            bag.Add(Potion, 5);
            Assert.AreEqual(1, bag.Split(0, 2));
            Assert.AreEqual("P3 P2 - -", Show(bag));
            Assert.AreEqual(-1, bag.Split(0, 3), "all of it is not a split");
            Assert.AreEqual(3, bag.Split(1, 1, 3));
            Assert.AreEqual("P3 P1 - P1", Show(bag));
            Assert.AreEqual(-1, bag.Split(0, 1, 1), "the target is taken");
        }

        [Test]
        public void Sort_merges_the_stacks_and_puts_them_in_order()
        {
            var bag = Bag(6);
            bag.Add(Potion, 3);
            bag.Add(Coin, 5);
            bag.Add(Potion, 6);
            bag.Add(Sword);
            bag.Sort();
            Assert.AreEqual("S1 P5 P4 C5 - -", Show(bag)); // by id: sword, potion, coin; the potions merged to 5 + 4
            var changes = 0;
            bag.Changed += _ => changes++;
            bag.Sort();
            Assert.AreEqual(0, changes, "already in order: nothing changed");
        }

        [Test]
        public void TransferTo_moves_as_much_as_fits()
        {
            var a = Bag(2);
            var b = Bag(1);
            a.Add(Potion, 8);
            Assert.AreEqual(5, a.TransferTo(b, Potion));
            Assert.AreEqual(3, a.CountOf(Potion));
            Assert.AreEqual(5, b.CountOf(Potion));
            Assert.AreEqual(0, a.TransferTo(b, Potion), "no room");
            Assert.AreEqual(0, a.TransferTo(a, Potion));
        }

        // ── events ──

        [Test]
        public void An_operation_raises_Changed_once_and_one_that_changes_nothing_raises_none()
        {
            var bag = Bag(1);
            var log = new List<InventoryChange>();
            bag.Changed += log.Add;
            bag.Add(Potion, 3);
            bag.Add(Sword, 1); // no room
            bag.Remove(Potion, 1);
            bag.Remove(Coin, 1); // there is none
            bag.Clear();
            bag.Clear(); // already empty
            CollectionAssert.AreEqual(
                new[] { InventoryChangeKind.Added, InventoryChangeKind.Removed, InventoryChangeKind.Reset },
                log.Select(c => c.Kind));
            Assert.AreEqual(3, log[0].Count);
            Assert.AreEqual(Potion, log[0].ItemId);
        }

        // ── saving ──

        [Test]
        public void A_saved_inventory_comes_back_slot_for_slot()
        {
            var bag = Bag();
            bag.Add(Potion, 8);
            bag.Add(Sword);
            bag.Move(0, 3);
            var save = bag.Save();
            var json = UnityEngine.JsonUtility.ToJson(save);

            var again = Bag();
            var lost = again.Load(UnityEngine.JsonUtility.FromJson<InventorySave>(json));
            Assert.AreEqual(0, lost);
            Assert.AreEqual(Show(bag), Show(again));
        }

        [Test]
        public void Loading_drops_what_the_game_no_longer_has_and_cuts_a_stack_to_the_limit_and_loses_what_does_not_fit()
        {
            var save = new InventorySave { slots = 4 };
            save.stacks.Add(new SavedStack { slot = 0, item = Potion.ToString("D"), count = 9 }); // the limit is 5 now
            save.stacks.Add(new SavedStack { slot = 1, item = Coin.ToString("D"), count = 7 });   // deleted in Loreman
            save.stacks.Add(new SavedStack { slot = 2, item = Sword.ToString("D"), count = 1 });
            save.stacks.Add(new SavedStack { slot = 9, item = Sword.ToString("D"), count = 1 });  // a slot this inventory does not have: the first free one
            save.stacks.Add(new SavedStack { slot = 3, item = "not a guid", count = 1 });
            var bag = Bag();
            var lost = bag.Load(save, id => id != Coin);
            Assert.AreEqual(4 + 7, lost);
            Assert.AreEqual("P5 S1 S1 -", Show(bag));

            var small = Bag(1);
            Assert.AreEqual(4 + 7 + 1 + 1, small.Load(save, id => id != Coin));
            Assert.AreEqual("P5", Show(small));
        }

        [Test]
        public void An_unlimited_inventory_loads_its_stacks_in_order()
        {
            var save = new InventorySave { slots = 0 };
            save.stacks.Add(new SavedStack { slot = 0, item = Potion.ToString("D"), count = 3 });
            save.stacks.Add(new SavedStack { slot = 1, item = Sword.ToString("D"), count = 1 });
            var bag = Bag(0);
            Assert.AreEqual(0, bag.Load(save));
            Assert.AreEqual("P3 S1", Show(bag));
        }

        [Test]
        public void Fixed_rules_and_a_rule_that_answers_zero_are_never_below_one()
        {
            Assert.AreEqual(1, new FixedStackRules(0).MaxStack(Sword));
            Assert.AreEqual(99, new FixedStackRules(99).MaxStack(Sword));
            var bag = new Inventory(new FixedStackRules(0), 2);
            bag.Add(Potion, 2);
            Assert.AreEqual("P1 P1", Show(bag));
        }
    }
}
