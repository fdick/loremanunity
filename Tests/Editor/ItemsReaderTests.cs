using System;
using System.Linq;
using Loreman.Dialogues.Editor;
using Loreman.Items;
using NUnit.Framework;

namespace Loreman.Dialogues.Tests
{
    /// <summary>The catalog of the items as Loreman's own tests write it (Tests/Fixtures/items.lorebuild, see <see cref="SampleBuild.OpenItems"/>).</summary>
    public class ItemsReaderTests
    {
        internal static ItemsData Fixture()
        {
            using (var file = SampleBuild.OpenItems()) return ItemsReader.Read(file.Read(file.Manifest.items.catalog));
        }

        [Test]
        public void The_manifest_of_a_build_with_items_says_what_they_are()
        {
            using (var file = SampleBuild.OpenItems())
            {
                var items = file.Manifest.items;
                Assert.IsTrue(items.IsPresent);
                Assert.AreEqual("items/items.bin", items.catalog);
                Assert.AreEqual(3, items.count);
                Assert.AreEqual(2, items.folders);
                Assert.AreEqual(1, items.icons.Count);
                Assert.AreEqual(1, items.skippedIcons);
                Assert.IsNotEmpty(items.hash);
                Assert.IsTrue(file.Has(items.icons[0]));
                Assert.IsTrue(file.Has(LoreBuildFile.ItemTextsPath("en")));
            }
        }

        [Test]
        public void A_manifest_without_items_has_none()
        {
            using (var file = SampleBuild.Open()) Assert.IsFalse(file.Manifest.items.IsPresent);
        }

        [Test]
        public void The_number_parameters_are_listed_once_with_their_units()
        {
            var data = Fixture();
            CollectionAssert.AreEqual(new[] { "Damage pt", "Heal hp", "Weight kg" }, data.Stats.Select(s => s.Name + " " + s.Unit));
        }

        [Test]
        public void The_folders_come_parent_first_and_the_items_point_into_them()
        {
            var data = Fixture();
            CollectionAssert.AreEqual(new[] { "Weapons", "Swords" }, data.Folders.Select(f => f.Name));
            Assert.AreEqual(-1, data.Folders[0].Parent);
            Assert.AreEqual(0, data.Folders[1].Parent);
            var inSwords = data.Items.Where(i => i.Folder == 1).ToList();
            Assert.AreEqual(1, inSwords.Count);
            Assert.AreEqual(2, data.Items.Count(i => i.Folder == -1), "the potion and the letter lie at the top");
        }

        [Test]
        public void An_item_has_its_numbers_texts_and_picture()
        {
            var data = Fixture();
            var sword = data.Items.Single(i => i.Folder == 1);
            Assert.IsFalse(sword.Name.IsNone);
            Assert.IsFalse(sword.Description.IsNone);
            Assert.AreEqual(0, sword.Icon);
            Assert.AreEqual(new[] { ("Damage", 15d), ("Weight", 2.5) }, sword.Stats.Select(s => (data.Stats[s.Stat].Name, s.Value)).ToArray());
            Assert.AreEqual("Lore", sword.Fields.Single().Name);
            Assert.IsFalse(sword.Fields.Single().Text.IsNone);
        }

        [Test]
        public void A_negative_whole_number_and_a_zero_are_read_back_exactly_and_an_item_without_a_picture_or_description_says_so()
        {
            var data = Fixture();
            var potion = data.Items.Single(i => i.Stats.Any(s => data.Stats[s.Stat].Name == "Heal"));
            Assert.AreEqual(-30d, potion.Stats.Single(s => data.Stats[s.Stat].Name == "Heal").Value);
            Assert.AreEqual(0d, potion.Stats.Single(s => data.Stats[s.Stat].Name == "Damage").Value);
            Assert.IsTrue(potion.Description.IsNone);
            Assert.AreEqual(0, potion.Icon, "the potion shares the sword's picture");
            var letter = data.Items.Single(i => i.Fields.Any(f => f.Name == "Text"));
            Assert.AreEqual(-1, letter.Icon, "a webp picture is not in the build");
        }

        [Test]
        public void The_words_of_the_items_are_in_the_table_of_each_language_under_the_keys_the_catalog_holds()
        {
            using (var file = SampleBuild.OpenItems())
            {
                var data = ItemsReader.Read(file.Read(file.Manifest.items.catalog));
                var en = StringTableReader.Read(file.Read(LoreBuildFile.ItemTextsPath("en"))).ToDictionary(e => e.Id, e => e.Text);
                var ru = StringTableReader.Read(file.Read(LoreBuildFile.ItemTextsPath("ru"))).ToDictionary(e => e.Id, e => e.Text);
                var sword = data.Items.Single(i => i.Folder == 1);
                Assert.AreEqual("Sword", en[sword.Name.Id]);
                Assert.AreEqual("Sharp.", en[sword.Description.Id]);
                Assert.AreEqual("Forged in the north.", en[sword.Fields[0].Text.Id]);
                Assert.AreEqual("Меч", ru[sword.Name.Id]);
                Assert.IsFalse(ru.ContainsKey(sword.Description.Id), "not translated: the game falls back to the base language");
            }
        }

        [Test]
        public void Damaged_data_is_refused_with_a_message_not_an_exception_from_the_depths()
        {
            using (var file = SampleBuild.OpenItems())
            {
                var good = file.Read(file.Manifest.items.catalog);
                Assert.Throws<DialogueFormatException>(() => ItemsReader.Read(new byte[] { 1, 2, 3 }));
                Assert.Throws<DialogueFormatException>(() => ItemsReader.Read(good.Take(good.Length - 5).ToArray()), "cut short");
                var wrongVersion = (byte[])good.Clone();
                wrongVersion[4] = 99;
                Assert.Throws<DialogueFormatException>(() => ItemsReader.Read(wrongVersion));
                Assert.Throws<DialogueFormatException>(() => ItemsReader.Read(good.Concat(new byte[] { 0 }).ToArray()), "bytes left over");
            }
        }
    }
}
