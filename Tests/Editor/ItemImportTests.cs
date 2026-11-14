using System;
using System.Linq;
using Loreman.Dialogues.Editor;
using Loreman.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Loreman.Dialogues.Tests
{
    /// <summary>The real thing, for the items: syncs the items build into a throw-away folder and looks at what Unity has afterwards. It cleans up after itself.</summary>
    public class ItemImportTests
    {
        private const string Folder = "Assets/LoremanItemImportTests";
        private LoremanSettings _settings;
        private Guid _project;

        [SetUp]
        public void SetUp()
        {
            using (var file = SampleBuild.OpenItems()) _project = Guid.Parse(file.Manifest.projectId);
            var existing = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.ItemsTable(_project));
            if (existing != null && !AssetDatabase.GetAssetPath(existing).StartsWith(Folder))
                Assert.Ignore("This project already has these items (" + AssetDatabase.GetAssetPath(existing) + "); these tests would overwrite them.");
            Cleanup();
            _settings = ScriptableObject.CreateInstance<LoremanSettings>();
            _settings.outputFolder = Folder + "/Generated";
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) UnityEngine.Object.DestroyImmediate(_settings);
            Cleanup();
        }

        private static void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
                if (aa != null)
                    foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { Folder }))
                        aa.RemoveAssetEntry(guid, false);
                AssetDatabase.DeleteAsset(Folder);
            }
            AssetDatabase.Refresh();
        }

        private UnitySyncTarget Target() => new UnitySyncTarget(_settings, _project);

        private SyncReport Sync(bool force = false)
        {
            using (var file = SampleBuild.OpenItems())
                return BuildSynchronizer.Run(file, Target(), force);
        }

        [Test]
        public void A_sync_makes_the_item_catalog_its_table_and_the_pictures_and_registers_them_in_addressables()
        {
            var report = Sync();
            Assert.IsTrue(report.ItemsWritten);
            Assert.AreEqual(3, report.Items);

            string hash;
            string iconCode;
            using (var file = SampleBuild.OpenItems())
            {
                hash = file.Manifest.items.hash;
                iconCode = System.IO.Path.GetFileNameWithoutExtension(file.Manifest.items.icons[0]);
            }

            var catalogPath = _settings.ItemCatalogPath(_project);
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(catalogPath);
            Assert.IsNotNull(catalog);
            Assert.AreEqual(hash, catalog.ContentHash);
            Assert.AreEqual(_project, catalog.ProjectGuid);
            Assert.AreEqual("en", catalog.BaseLanguage);
            Assert.AreEqual(3, catalog.Data.Items.Count);
            Assert.AreEqual(hash, Target().ItemsHash);

            // Addressables: the catalog by address and label, the picture by address, both in the items group
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var catalogEntry = aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(catalogPath));
            Assert.AreEqual(LoremanNames.ItemCatalogAddress(_project), catalogEntry.address);
            Assert.IsTrue(catalogEntry.labels.Contains(LoremanNames.ItemCatalogLabel), "the game finds the item catalogs by this label");
            Assert.AreEqual(_settings.itemsAddressablesGroup, catalogEntry.parentGroup.Name);

            var iconPath = _settings.ItemIconsFolder(_project) + "/" + iconCode + ".png";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(iconPath), "the picture is a sprite");
            var iconEntry = aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(iconPath));
            Assert.AreEqual(LoremanNames.ItemIconAddress(_project, iconCode), iconEntry.address);

            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.IsFalse(importer.mipmapEnabled);
        }

        [Test]
        public void The_words_of_the_items_are_in_one_table_per_language_and_a_missing_translation_is_missing()
        {
            Sync();
            var collection = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.ItemsTable(_project));
            Assert.IsNotNull(collection);
            var data = ItemsReaderTests.Fixture();
            var sword = data.Items.Single(i => i.Folder == 1);

            var en = collection.StringTables.Single(t => t.LocaleIdentifier.Code == "en");
            var ru = collection.StringTables.Single(t => t.LocaleIdentifier.Code == "ru");
            Assert.AreEqual("Sword", en.GetEntry(sword.Name.Id).Value);
            Assert.AreEqual("Sharp.", en.GetEntry(sword.Description.Id).Value);
            Assert.AreEqual("Меч", ru.GetEntry(sword.Name.Id).Value);
            Assert.IsNull(ru.GetEntry(sword.Description.Id), "not translated: the game falls back to the base language");
        }

        [Test]
        public void Syncing_the_same_build_again_changes_nothing()
        {
            Sync();
            var report = Sync();
            Assert.IsTrue(report.WasUpToDate);
            Assert.IsFalse(report.ItemsWritten);
        }

        [Test]
        public void A_forced_sync_rewrites_the_items_without_duplicating_anything()
        {
            Sync();
            var report = Sync(force: true);
            Assert.IsTrue(report.ItemsWritten);
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:ItemCatalog", new[] { Folder }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:Sprite", new[] { _settings.ItemIconsFolder(_project) }).Length);
            Assert.AreEqual(1, LocalizationEditorSettings.GetStringTableCollections().Count(c => c.TableCollectionName == LoremanNames.ItemsTable(_project)));
        }

        [Test]
        public void A_deleted_picture_makes_the_items_count_as_missing_so_the_next_sync_makes_them_again()
        {
            Sync();
            var folder = _settings.ItemIconsFolder(_project);
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            Assert.AreEqual(string.Empty, Target().ItemsHash);
            var report = Sync();
            Assert.IsTrue(report.ItemsWritten);
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:Sprite", new[] { folder }).Length);
        }

        [Test]
        public void Removing_the_items_takes_the_catalog_the_pictures_and_the_table_away()
        {
            Sync();
            var target = Target();
            target.Begin();
            try
            {
                target.RemoveItems();
            }
            finally
            {
                target.End();
            }
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<ItemCatalog>(_settings.ItemCatalogPath(_project)));
            Assert.IsFalse(AssetDatabase.IsValidFolder(_settings.ItemIconsFolder(_project)));
            Assert.IsNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.ItemsTable(_project)));
            Assert.AreEqual(string.Empty, target.ItemsHash);
        }

        [Test]
        public void A_picture_is_imported_as_a_sprite_that_can_be_loaded_by_the_game_by_its_address_prefix()
        {
            Sync();
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var entries = aa.groups.SelectMany(g => g.entries).Where(e => e.address.StartsWith("loreman/items/" + _project.ToString("N") + "/icons/")).ToList();
            Assert.AreEqual(1, entries.Count);
        }
    }
}
