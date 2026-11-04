using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using Loreman.Dialogues.Editor;

namespace Loreman.Dialogues.Tests
{
    /// <summary>How the dialogue pickers list, order, search and name dialogues.</summary>
    public class DialogueSearchTests
    {
        private static CatalogDialogue D(string id, string name, string group = null, int nodes = 3) =>
            new CatalogDialogue { id = id, name = name, groupId = group, nodes = nodes };

        private static readonly List<CatalogGroup> Groups = new List<CatalogGroup>
        {
            new CatalogGroup { id = "g-town", name = "Town" },
            new CatalogGroup { id = "g-cave", name = "Cave" },
        };

        private static List<DialogueEntry> Sample() => DialogueSearch.Entries(Groups, new List<CatalogDialogue>
        {
            D("1", "Merchant", "g-town"),
            D("2", "city gate", "g-town"),
            D("3", "Bat swarm", "g-cave"),
            D("4", "Tutorial"),
            D("5", "Intro", "g-gone"), // a group the catalog does not list: the dialogue is shown without one
            D("6", "Gate keeper", "g-cave"),
        });

        // ── order ──

        [Test]
        public void Dialogues_are_ordered_by_group_then_name_ignoring_case_and_the_ungrouped_come_last()
        {
            CollectionAssert.AreEqual(
                new[] { "Bat swarm", "Gate keeper", "city gate", "Merchant", "Intro", "Tutorial" },
                Sample().Select(e => e.Name));
        }

        [Test]
        public void A_dialogue_shows_its_group_in_its_full_name()
        {
            var entries = Sample();
            Assert.AreEqual("Town / Merchant", entries.Single(e => e.Id == "1").FullName);
            Assert.AreEqual("Tutorial", entries.Single(e => e.Id == "4").FullName);
            Assert.AreEqual("Intro", entries.Single(e => e.Id == "5").FullName);
        }

        // ── search ──

        [Test]
        public void An_empty_query_keeps_everything_in_order()
        {
            var entries = Sample();
            CollectionAssert.AreEqual(entries.Select(e => e.Id), DialogueSearch.Filter(entries, "").Select(e => e.Id));
            CollectionAssert.AreEqual(entries.Select(e => e.Id), DialogueSearch.Filter(entries, "   ").Select(e => e.Id));
            CollectionAssert.AreEqual(entries.Select(e => e.Id), DialogueSearch.Filter(entries, null).Select(e => e.Id));
        }

        [Test]
        public void The_search_ignores_case_and_finds_a_part_of_the_name()
        {
            CollectionAssert.AreEqual(new[] { "Merchant" }, DialogueSearch.Filter(Sample(), "CHANT").Select(e => e.Name));
        }

        [Test]
        public void The_group_is_searched_too()
        {
            CollectionAssert.AreEquivalent(new[] { "Bat swarm", "Gate keeper" }, DialogueSearch.Filter(Sample(), "cave").Select(e => e.Name));
        }

        [Test]
        public void Every_word_must_match_in_any_order_across_the_group_and_the_name()
        {
            CollectionAssert.AreEqual(new[] { "city gate" }, DialogueSearch.Filter(Sample(), "gate town").Select(e => e.Name));
            CollectionAssert.AreEqual(new[] { "city gate" }, DialogueSearch.Filter(Sample(), "town gate").Select(e => e.Name));
            Assert.IsEmpty(DialogueSearch.Filter(Sample(), "gate merchant"));
        }

        [Test]
        public void A_name_that_begins_with_the_query_comes_before_one_that_only_contains_it()
        {
            // "Gate keeper" begins with "gate"; "city gate" only has it inside
            CollectionAssert.AreEqual(new[] { "Gate keeper", "city gate" }, DialogueSearch.Filter(Sample(), "gate").Select(e => e.Name));
        }

        [Test]
        public void A_match_through_the_name_comes_before_a_match_only_through_the_group()
        {
            var entries = DialogueSearch.Entries(Groups, new List<CatalogDialogue>
            {
                D("a", "Aardvark", "g-cave"),   // "cave" only through its group
                D("b", "Old cave map", "g-town"), // "cave" in its name
            });
            CollectionAssert.AreEqual(new[] { "Old cave map", "Aardvark" }, DialogueSearch.Filter(entries, "cave").Select(e => e.Name));
        }

        [Test]
        public void Nothing_matches_gives_an_empty_list_and_matches_says_the_same()
        {
            var entries = Sample();
            Assert.IsEmpty(DialogueSearch.Filter(entries, "zzz"));
            Assert.IsFalse(DialogueSearch.Matches(entries[0], "zzz"));
            Assert.IsTrue(DialogueSearch.Matches(entries[0], ""));
            Assert.IsTrue(DialogueSearch.Matches(entries[0], "bat"));
        }

        // ── the label of the field ──

        [Test]
        public void The_field_shows_none_the_full_name_or_that_the_dialogue_is_missing()
        {
            var entries = Sample();
            Assert.AreEqual("(none)", DialogueSearch.Label(entries, ""));
            Assert.AreEqual("(none)", DialogueSearch.Label(entries, null));
            Assert.AreEqual("Town / Merchant", DialogueSearch.Label(entries, "1"));
            Assert.AreEqual("(missing) 77", DialogueSearch.Label(entries, "77"));
        }

        [Test]
        public void An_id_is_found_whatever_its_case()
        {
            var entries = DialogueSearch.Entries(Groups, new List<CatalogDialogue> { D("ABC-1", "Upper") });
            Assert.AreEqual("Upper", DialogueSearch.Label(entries, "abc-1"));
        }

        // ── the picker window (as it is built; the click and the keys are the editor's) ──

        [Test]
        public void The_picker_lists_every_dialogue_narrows_while_typing_and_hands_back_the_id_of_the_chosen_one()
        {
            string picked = null;
            var window = DialoguePickerWindow.Create(Sample(), "1", id => picked = id);
            try
            {
                window.CreateGUI();
                Assert.AreEqual(6, window.Shown.Count);

                var search = window.rootVisualElement.Q<UnityEditor.UIElements.ToolbarSearchField>();
                Assert.IsNotNull(search);
                window.Refilter("gate"); // what the search box calls when its text changes (a window that is not shown has no panel to send the event)
                Assert.AreEqual(2, window.Shown.Count);
                Assert.AreEqual("Gate keeper", window.Shown[0].Name);

                window.Choose(1);
                Assert.AreEqual("2", picked);
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Choosing_nothing_that_is_listed_does_not_call_back()
        {
            string picked = null;
            var window = DialoguePickerWindow.Create(Sample(), "", id => picked = id);
            try
            {
                window.CreateGUI();
                window.Refilter("zzz");
                Assert.AreEqual(0, window.Shown.Count);
                window.Choose(0);
                Assert.IsNull(picked);
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
