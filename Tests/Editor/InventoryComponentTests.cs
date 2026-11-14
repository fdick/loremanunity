using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loreman.Dialogues.Editor;
using Loreman.Items;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Loreman.Dialogues.Tests
{
    /// <summary>The component around the inventory, with a database made of the fixture items and no Addressables.</summary>
    public class InventoryComponentTests
    {
        private readonly List<Object> _made = new List<Object>();
        private ItemDatabase _db;
        private ItemReference _sword, _potion;

        [SetUp]
        public void SetUp()
        {
            var loader = new FakeLoader();
            using (var file = SampleBuild.OpenItems())
            {
                var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                _made.Add(catalog);
                catalog.Populate(file.Manifest.projectId, "Game", file.Manifest.items.hash, "en", file.Read(file.Manifest.items.catalog));
                loader.Assets[LoremanNames.ItemCatalogAddress(Guid.Parse(file.Manifest.projectId))] = catalog;
                _db = new ItemDatabase(loader, new FakeItemTexts(file));
            }
            _db.InitializeAsync().Wait();
            _db.StackRule = item => item.Name == "Potion" ? 5 : 1;
            _sword = _db.Find("Sword");
            _potion = _db.Find("Potion");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private InventoryComponent Make(int slots, params (string Item, int Count)[] start)
        {
            var go = new GameObject("bag");
            _made.Add(go);
            var component = go.AddComponent<InventoryComponent>();
            var so = new UnityEditor.SerializedObject(component);
            so.FindProperty("slots").intValue = slots;
            var list = so.FindProperty("startingItems");
            list.arraySize = start.Length;
            for (var i = 0; i < start.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item.id").stringValue = _db.Find(start[i].Item).Id.ToString("D");
                element.FindPropertyRelative("count").intValue = start[i].Count;
            }
            so.ApplyModifiedProperties();
            component.UseDatabase(_db);
            component.InitializeAsync().Wait();
            return component;
        }

        [Test]
        public void An_inventory_starts_with_what_was_picked_in_the_inspector()
        {
            var bag = Make(4, ("Potion", 7), ("Sword", 1));
            Assert.IsTrue(bag.IsReady);
            Assert.AreEqual(7, bag.CountOf(_potion));
            Assert.AreEqual(1, bag.CountOf("sword"));
            Assert.AreEqual("Potion", bag.ItemAt(0).Name);
            Assert.IsNull(bag.ItemAt(3));
        }

        [Test]
        public void Items_are_added_and_removed_by_item_by_reference_and_by_name()
        {
            var bag = Make(2);
            var item = _db.Find("Potion");
            Assert.AreEqual(0, bag.Add(item, 3));
            Assert.AreEqual(0, bag.Add(_potion, 2));
            Assert.AreEqual(0, bag.Add("Potion", 0));
            Assert.AreEqual(1, bag.Add("Sword", 2), "one sword fits, one does not");
            Assert.IsTrue(bag.Has("Potion", 5));
            Assert.IsFalse(bag.Has("Potion", 6));
            Assert.IsFalse(bag.Has("Banana"), "an item no one has the name of is simply not there");
            Assert.IsTrue(bag.TryRemove("Potion", 5));
            Assert.AreEqual(0, bag.Remove(_potion, 1));
            Assert.IsFalse(bag.TryAdd(_sword, 2));
            Assert.AreEqual(1, bag.CountOf(_sword));
            Assert.Throws<KeyNotFoundException>(() => bag.Add("Banana"));
        }

        [Test]
        public void Changed_is_raised_with_what_happened_and_so_is_the_event_of_the_inspector()
        {
            var bag = Make(3);
            var changes = new List<InventoryChange>();
            bag.Changed += changes.Add;
            bag.Add(_potion, 2);
            bag.Remove(_potion, 1);
            CollectionAssert.AreEqual(new[] { InventoryChangeKind.Added, InventoryChangeKind.Removed }, changes.Select(c => c.Kind));
            Assert.AreEqual(1, changes[1].Count);
        }

        [Test]
        public void An_inventory_that_is_not_ready_says_so_instead_of_failing_in_the_depths()
        {
            var go = new GameObject("bag");
            _made.Add(go);
            var bag = go.AddComponent<InventoryComponent>();
            Assert.IsFalse(bag.IsReady);
            var e = Assert.Throws<InvalidOperationException>(() => bag.Add(_db.Find("Potion")));
            StringAssert.Contains("not ready", e.Message);
        }

        [Test]
        public void The_database_cannot_be_replaced_once_the_inventory_has_started()
        {
            var bag = Make(1);
            Assert.Throws<InvalidOperationException>(() => bag.UseDatabase(_db));
        }

        [Test]
        public void A_save_comes_back_and_what_the_project_no_longer_has_is_dropped()
        {
            var bag = Make(4, ("Potion", 7), ("Sword", 1));
            var json = bag.SaveToJson();
            StringAssert.Contains(_db.Find("Potion").Id.ToString("D"), json);

            var other = Make(4);
            Assert.AreEqual(0, other.LoadFromJson(json));
            Assert.AreEqual(7, other.CountOf(_potion));
            Assert.AreEqual(1, other.CountOf(_sword));

            var gone = new InventorySave { slots = 4 };
            gone.stacks.Add(new SavedStack { slot = 0, item = Guid.NewGuid().ToString("D"), count = 3 });
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not in the project any more"));
            Assert.AreEqual(3, other.LoadFromJson(JsonUtility.ToJson(gone)));
            Assert.AreEqual(0, other.CountOf(_potion));
            Assert.Throws<ArgumentException>(() => other.LoadFromJson(""));
        }

        [Test]
        public void One_inventory_gives_to_another_as_far_as_it_fits()
        {
            var a = Make(2, ("Potion", 8));
            var b = Make(1);
            Assert.AreEqual(5, a.Give(b, _db.Find("Potion")));
            Assert.AreEqual(3, a.CountOf(_potion));
            Assert.AreEqual(5, b.CountOf(_potion));
        }
    }
}
