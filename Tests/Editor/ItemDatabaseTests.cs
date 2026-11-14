using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loreman.Dialogues.Editor;
using Loreman.Items;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Loreman.Dialogues.Tests
{
    /// <summary>The words of the items from the tables of the fixture build, in a language that can be switched, so the tests need neither Addressables nor Localization.</summary>
    internal sealed class FakeItemTexts : IItemTextSource
    {
        private readonly Dictionary<string, Dictionary<long, string>> _languages = new Dictionary<string, Dictionary<long, string>>();
        public string Language = "en";
        public string BaseLanguage = "en";
        public Guid Project;
        public int Initialized;
        public bool Disposed;

        public FakeItemTexts(LoreBuildFile file)
        {
            foreach (var l in file.Manifest.languages)
            {
                var path = LoreBuildFile.ItemTextsPath(l.code);
                if (file.Has(path)) _languages[l.code] = StringTableReader.Read(file.Read(path)).ToDictionary(e => e.Id, e => e.Text);
            }
        }

        public event Action TextsChanged;

        public Task InitializeAsync(IReadOnlyList<ItemCatalog> catalogs, CancellationToken cancellationToken)
        {
            Initialized++;
            Project = catalogs.Count > 0 ? catalogs[0].ProjectGuid : Guid.Empty;
            return Task.CompletedTask;
        }

        public string GetText(Guid projectId, TextKey key)
        {
            if (_languages.TryGetValue(Language, out var current) && current.TryGetValue(key.Id, out var text)) return text;
            return _languages.TryGetValue(BaseLanguage, out var fallback) && fallback.TryGetValue(key.Id, out var t) ? t : string.Empty;
        }

        public void Switch(string language)
        {
            Language = language;
            TextsChanged?.Invoke();
        }

        public void Dispose() => Disposed = true;
    }

    public class ItemDatabaseTests
    {
        private readonly List<Object> _made = new List<Object>();
        private FakeLoader _loader;
        private FakeItemTexts _texts;
        private ItemCatalog _catalog;
        private Guid _project;
        private string _iconAddress;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _loader = new FakeLoader();
            using (var file = SampleBuild.OpenItems())
            {
                _project = Guid.Parse(file.Manifest.projectId);
                _catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                _catalog.Populate(file.Manifest.projectId, file.Manifest.projectName, file.Manifest.items.hash, file.Manifest.baseLanguage, file.Read(file.Manifest.items.catalog));
                _texts = new FakeItemTexts(file) { BaseLanguage = file.Manifest.baseLanguage, Language = file.Manifest.baseLanguage };
                _iconAddress = LoremanNames.ItemIconAddress(_project, System.IO.Path.GetFileNameWithoutExtension(file.Manifest.items.icons[0]));
            }
            _made.Add(_catalog);
            var texture = new Texture2D(2, 2);
            _made.Add(texture);
            _sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _made.Add(_sprite);
            _loader.Assets[LoremanNames.ItemCatalogAddress(_project)] = _catalog;
            _loader.Assets[_iconAddress] = _sprite;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private static T Run<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "the task did not finish");
            return task.Result;
        }

        private static void Run(Task task) => Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "the task did not finish");

        private ItemDatabase Database()
        {
            var db = new ItemDatabase(_loader, _texts);
            Run(db.InitializeAsync());
            return db;
        }

        // ── loading ──

        [Test]
        public void The_database_lists_every_item_of_every_catalog_after_it_is_loaded()
        {
            var db = new ItemDatabase(_loader, _texts);
            Assert.IsFalse(db.IsReady);
            Assert.AreEqual(0, db.Count);
            Run(db.InitializeAsync());
            Assert.IsTrue(db.IsReady);
            Assert.AreEqual(3, db.Count);
            Assert.AreEqual(1, _texts.Initialized);
            CollectionAssert.AreEquivalent(new[] { "Sword", "Potion", "Letter" }, db.All.Select(i => i.Name));
            Assert.AreEqual(2, db.Folders.Count);
        }

        [Test]
        public void Initializing_twice_loads_once()
        {
            var db = new ItemDatabase(_loader, _texts);
            var first = db.InitializeAsync();
            var second = db.InitializeAsync();
            Assert.AreSame(first, second);
            Run(first);
            Assert.AreEqual(1, _loader.Loads.Count(l => l == LoremanNames.ItemCatalogAddress(_project)));
        }

        [Test]
        public void A_project_without_item_catalogs_gives_an_empty_database()
        {
            _loader.Assets.Clear();
            var db = Database();
            Assert.IsTrue(db.IsReady);
            Assert.AreEqual(0, db.Count);
        }

        // ── asking ──

        [Test]
        public void An_item_is_found_by_id_by_reference_and_by_name_in_any_case()
        {
            var db = Database();
            var sword = db.Find("sword");
            Assert.IsNotNull(sword);
            Assert.AreSame(sword, db.Get(sword.Id));
            Assert.AreSame(sword, db.Get(new ItemReference(sword.Id)));
            Assert.AreSame(sword, db.Get((ItemReference)sword));
            Assert.AreSame(sword, db.Find("  SWORD "));
            Assert.IsNull(db.Find("Banana"));
            Assert.IsNull(db.Find(""));
            Assert.IsFalse(db.TryGet(Guid.NewGuid(), out _));
            Assert.Throws<KeyNotFoundException>(() => db.Get(Guid.NewGuid()));
        }

        [Test]
        public void An_item_knows_its_description_numbers_text_parameters_and_folder()
        {
            var db = Database();
            var sword = db.Find("Sword");
            Assert.AreEqual("Sharp.", sword.Description);
            Assert.AreEqual(15d, sword.GetStat("Damage"));
            Assert.AreEqual(15d, sword.GetStat(" damage "), "case and spaces do not matter");
            Assert.AreEqual(2.5, sword.GetStat("Weight"));
            Assert.AreEqual(7d, sword.GetStat("Armor", 7d));
            Assert.IsTrue(sword.HasStat("Weight"));
            Assert.IsFalse(sword.HasStat("Armor"));
            Assert.AreEqual("15 pt", sword.Stats.First(s => s.Name == "Damage").ToString());
            Assert.AreEqual("2.5 kg", sword.Stats.First(s => s.Name == "Weight").ToString());
            Assert.AreEqual("Forged in the north.", sword.GetField("lore"));
            Assert.IsTrue(sword.HasField("Lore"));
            Assert.AreEqual("", sword.GetField("Missing"));
            Assert.AreEqual("Weapons/Swords", sword.Folder.Path);
            Assert.AreEqual("Weapons", sword.Folder.Parent.Name);
            Assert.IsNull(db.Find("Potion").Folder);
            Assert.AreEqual("", db.Find("Potion").Description);
            Assert.AreEqual(_project, sword.ProjectId);
        }

        [Test]
        public void Folders_list_their_items_with_or_without_the_folders_below()
        {
            var db = Database();
            CollectionAssert.AreEquivalent(new[] { "Sword" }, db.InFolder("Weapons/Swords").Select(i => i.Name));
            CollectionAssert.AreEquivalent(new[] { "Sword" }, db.InFolder("weapons").Select(i => i.Name));
            Assert.AreEqual(0, db.InFolder("Weapons", includeSubfolders: false).Count);
            CollectionAssert.AreEquivalent(new[] { "Potion", "Letter" }, db.InFolder("", includeSubfolders: false).Select(i => i.Name));
            Assert.AreEqual(3, db.InFolder("").Count);
            Assert.AreEqual(0, db.InFolder("Armor").Count);
        }

        [Test]
        public void Search_needs_every_word_in_the_name_the_description_or_the_folder_and_puts_names_first()
        {
            var db = Database();
            CollectionAssert.AreEqual(new[] { "Sword" }, db.Search("sw").Select(i => i.Name));
            CollectionAssert.AreEqual(new[] { "Sword" }, db.Search("sharp sword").Select(i => i.Name), "a word of the description counts");
            CollectionAssert.AreEqual(new[] { "Sword" }, db.Search("weapons").Select(i => i.Name), "a word of the folder counts");
            Assert.AreEqual(0, db.Search("sword banana").Count);
            Assert.AreEqual(3, db.Search("").Count);
            CollectionAssert.AreEqual(new[] { "Letter", "Potion", "Sword" }, db.Search("").Select(i => i.Name));
        }

        // ── language ──

        [Test]
        public void A_name_follows_the_language_and_falls_back_to_the_base_language_where_there_is_no_translation()
        {
            var db = Database();
            var sword = db.Find("Sword");
            var raised = 0;
            db.TextsChanged += () => raised++;

            _texts.Switch("ru");
            Assert.AreEqual(1, raised);
            Assert.AreEqual("Меч", sword.Name);
            Assert.AreEqual("Sharp.", sword.Description, "not translated: the base language fills it");
            Assert.AreEqual("Potion", db.Find("Potion").Name, "no Russian name: the base language");
            Assert.IsNotNull(db.Search("меч").FirstOrDefault(), "the search follows the language too");

            _texts.Switch("en");
            Assert.AreEqual("Sword", sword.Name);
        }

        // ── pictures ──

        [Test]
        public void A_picture_is_loaded_once_when_asked_for_and_shared_by_the_items_that_use_it()
        {
            var db = Database();
            var sword = db.Find("Sword");
            var potion = db.Find("Potion");
            Assert.IsTrue(sword.HasIcon);
            Assert.IsNull(sword.Icon, "not loaded yet");

            var icon = Run(sword.LoadIconAsync());
            Assert.AreSame(_sprite, icon);
            Assert.AreSame(_sprite, sword.Icon);
            Assert.AreSame(_sprite, Run(potion.LoadIconAsync()), "the potion has the same picture");
            Assert.AreEqual(1, _loader.Loads.Count(l => l == _iconAddress));
        }

        [Test]
        public void An_item_without_a_picture_loads_none_and_a_picture_that_fails_to_load_can_be_asked_for_again()
        {
            var db = Database();
            var letter = db.Find("Letter");
            Assert.IsFalse(letter.HasIcon);
            Assert.IsNull(Run(letter.LoadIconAsync()));
            Assert.IsNull(letter.Icon);

            _loader.Assets.Remove(_iconAddress);
            var sword = db.Find("Sword");
            Assert.Throws<AggregateException>(() => sword.LoadIconAsync().Wait(TimeSpan.FromSeconds(5)));
            _loader.Assets[_iconAddress] = _sprite;
            Assert.AreSame(_sprite, Run(sword.LoadIconAsync()), "the failed load is not remembered");
        }

        [Test]
        public void Disposing_lets_the_pictures_and_the_catalogs_go_and_empties_the_database()
        {
            var db = Database();
            Run(db.Find("Sword").LoadIconAsync());
            db.Dispose();
            Assert.IsTrue(_texts.Disposed);
            CollectionAssert.Contains(_loader.Released, _sprite);
            CollectionAssert.Contains(_loader.Released, _catalog);
            Assert.IsFalse(db.IsReady);
            Assert.AreEqual(0, db.Count);
            db.Dispose(); // twice is harmless
        }

        // ── stacking ──

        [Test]
        public void An_item_stacks_as_far_as_its_Max_Stack_number_says_and_not_at_all_without_one()
        {
            var db = Database();
            Assert.AreEqual(1, db.Find("Sword").MaxStack, "no Max Stack: one to a slot");
            db.StackRule = item => item.Name == "Potion" ? 20 : 1;
            Assert.AreEqual(20, db.Find("Potion").MaxStack);
            Assert.AreEqual(20, db.Rules.MaxStack(db.Find("Potion").Id));
            Assert.AreEqual(1, db.Rules.MaxStack(Guid.NewGuid()), "an unknown item does not stack");
            db.StackRule = null; // back to the default
            Assert.AreEqual(1, db.Find("Potion").MaxStack);
        }

        [Test]
        public void A_database_makes_the_rules_of_an_inventory()
        {
            var db = Database();
            db.StackRule = item => item.Name == "Potion" ? 3 : 1;
            var bag = new Inventory(db.Rules, 2);
            var potion = db.Find("Potion");
            Assert.AreEqual(1, bag.Add(potion.Id, 7)); // 3 + 3, one does not fit
            Assert.AreEqual(6, bag.CountOf(potion.Id));
        }

        [Test]
        public void The_names_of_stack_parameters_are_forgiving()
        {
            Assert.AreEqual("maxstack", ItemRules.Normalize("Max Stack"));
            Assert.AreEqual("maxstack", ItemRules.Normalize(" MAX  stack "));
            Assert.AreEqual("", ItemRules.Normalize(null));
        }

        // ── the shared database ──

        [Test]
        public void The_shared_database_is_made_once_and_can_be_replaced()
        {
            LoremanItems.Release();
            try
            {
                var mine = new ItemDatabase(_loader, _texts);
                LoremanItems.Use(mine);
                var first = Run(LoremanItems.GetAsync());
                var second = Run(LoremanItems.GetAsync());
                Assert.AreSame(mine, first);
                Assert.AreSame(first, second);
                Assert.IsTrue(LoremanItems.IsReady);
                Assert.AreEqual(3, first.Count);
            }
            finally
            {
                LoremanItems.Release();
            }
            Assert.IsNull(LoremanItems.Database);
            Assert.IsFalse(LoremanItems.IsReady);
        }

        [Test]
        public void An_item_reference_keeps_only_the_id()
        {
            var id = Guid.NewGuid();
            var reference = new ItemReference(id);
            Assert.IsTrue(reference.IsSet);
            Assert.AreEqual(id, reference.Guid);
            Guid asGuid = reference;
            Assert.AreEqual(id, asGuid);
            Assert.IsFalse(default(ItemReference).IsSet);
            Assert.IsFalse(new ItemReference(Guid.Empty).IsSet);
            Assert.AreEqual("(no item)", default(ItemReference).ToString());
        }
    }
}
