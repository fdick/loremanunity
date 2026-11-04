using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using System.Linq;
using Loreman.Dialogues.Editor;

namespace Loreman.Dialogues.Tests
{
    /// <summary>
    /// The real thing: syncs the sample build into a throw-away folder of the project and looks at what Unity has afterwards (assets, Addressables, Localization
    /// tables). It touches the project's Addressables and Localization settings, so it cleans up after itself.
    /// </summary>
    public class UnityImportTests
    {
        private const string Folder = "Assets/LoremanImportTests";
        private LoremanSettings _settings;

        [SetUp]
        public void SetUp()
        {
            Cleanup();
            _settings = ScriptableObject.CreateInstance<LoremanSettings>();
            _settings.outputFolder = Folder + "/Generated";
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_settings);
            Cleanup();
        }

        private static void Cleanup()
        {
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (aa != null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { Folder }.Where(AssetDatabase.IsValidFolder).ToArray()))
                    aa.RemoveAssetEntry(guid, false);
            }
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        private SyncReport Sync(bool force = false)
        {
            using (var file = SampleBuild.Open())
                return BuildSynchronizer.Run(file, new UnitySyncTarget(_settings), force);
        }

        [Test]
        public void A_sync_makes_the_catalog_the_dialogue_and_its_tables_and_registers_them_in_addressables()
        {
            var report = Sync();
            Assert.AreEqual(1, report.Added);

            DialogueCatalog catalog;
            Guid id;
            using (var file = SampleBuild.Open())
            {
                catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(_settings.CatalogPath);
                id = SampleBuild.Gate(file.Manifest).Guid;
                Assert.AreEqual(file.Manifest.contentHash, catalog.ContentHash);
            }
            Assert.IsNotNull(catalog);
            Assert.AreEqual("My Game!", catalog.ProjectName);
            Assert.AreEqual("en", catalog.BaseLanguage);
            CollectionAssert.AreEquivalent(new[] { "en", "ru", "de" }, catalog.Languages);
            Assert.IsTrue(catalog.TryGetDialogue(id, out var entry));
            Assert.AreEqual("Gate", entry.name);
            Assert.AreEqual(2, catalog.SpeakerCount);

            var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_settings.DialoguePath(id));
            Assert.IsNotNull(asset);
            Assert.AreEqual(id, asset.Id);
            Assert.AreEqual(3, DialogueGraphReader.Read(asset.GraphBytes).Nodes.Count);

            // Addressables: the catalog and the dialogue have their addresses and the label
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var dialogueEntry = aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(_settings.DialoguePath(id)));
            Assert.AreEqual(LoremanNames.DialogueAddress(id), dialogueEntry.address);
            Assert.IsTrue(dialogueEntry.labels.Contains(LoremanNames.Label));
            Assert.AreEqual(LoremanNames.CatalogAddress, aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(_settings.CatalogPath)).address);
        }

        [Test]
        public void The_texts_of_every_language_are_in_the_table_of_the_dialogue_and_a_missing_translation_is_missing()
        {
            Sync();
            using (var file = SampleBuild.Open())
            {
                var id = SampleBuild.Gate(file.Manifest).Guid;
                var g = SampleBuild.GateGraph(file);
                var collection = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(id));
                Assert.IsNotNull(collection);

                var en = (StringTable)collection.GetTable("en");
                var ru = (StringTable)collection.GetTable("ru");
                Assert.IsNotNull(collection.GetTable("de")); // a table for every language, even an empty one
                Assert.AreEqual("Halt! Who goes there?", en.GetEntry(g.Nodes[0].Text.Id).Value);
                Assert.AreEqual("Стой! Кто идёт?", ru.GetEntry(g.Nodes[0].Text.Id).Value);
                Assert.IsNull(ru.GetEntry(g.Nodes[2].Text.Id), "not translated: the game falls back to the base language");
                Assert.AreEqual("node:" + g.Nodes[0].Id.ToString("D"), collection.SharedData.GetEntry(g.Nodes[0].Text.Id).Key);

                var speakers = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.SpeakersTable);
                var guard = file.Manifest.speakers[g.Nodes[0].Speaker];
                Assert.AreEqual("Guard", ((StringTable)speakers.GetTable("en")).GetEntry(guard.nameKey).Value);
                Assert.AreEqual("Стражник", ((StringTable)speakers.GetTable("ru")).GetEntry(guard.nameKey).Value);
            }
        }

        [Test]
        public void The_voice_overs_are_audio_clips_in_the_asset_table_of_the_dialogue_one_for_each_language()
        {
            Sync();
            using (var file = SampleBuild.Open())
            {
                var id = SampleBuild.Gate(file.Manifest).Guid;
                var g = SampleBuild.GateGraph(file);
                var collection = LocalizationEditorSettings.GetAssetTableCollection(LoremanNames.AudioTable(id));
                Assert.IsNotNull(collection);

                var en = (AssetTable)collection.GetTable("en");
                var ru = (AssetTable)collection.GetTable("ru");
                var line = g.Nodes[0].Audio.Id;
                var answer = g.Nodes[1].Options[0].Audio.Id;
                Assert.IsNotNull(en.GetEntry(line), "the line has a voice-over in the base language");
                Assert.IsNotNull(ru.GetEntry(line));
                Assert.IsNull(en.GetEntry(answer), "the answer was recorded in Russian only");
                Assert.IsNotNull(ru.GetEntry(answer));

                // the entry points at the imported clip
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(ru.GetEntry(line).Guid));
                Assert.IsNotNull(clip);
                Assert.Greater(clip.length, 0f);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(_settings.AudioFolder(id) + "/en/" + g.Nodes[0].Id.ToString("N") + ".wav"));
            }
        }

        [Test]
        public void A_dialogue_that_is_gone_takes_its_voice_overs_with_it()
        {
            Sync();
            Guid id;
            using (var file = SampleBuild.Open()) id = SampleBuild.Gate(file.Manifest).Guid;
            new UnitySyncTarget(_settings).RemoveDialogue(id);
            AssetDatabase.Refresh();
            Assert.IsNull(LocalizationEditorSettings.GetAssetTableCollection(LoremanNames.AudioTable(id)));
            Assert.IsFalse(AssetDatabase.IsValidFolder(_settings.AudioFolder(id)));
        }

        [Test]
        public void The_locales_of_the_build_are_made_when_the_project_lacks_them()
        {
            Sync();
            var codes = LocalizationEditorSettings.GetLocales().Select(l => l.Identifier.Code).ToList();
            CollectionAssert.IsSupersetOf(codes, new[] { "en", "ru", "de" });
        }

        [Test]
        public void Syncing_the_same_build_again_changes_nothing()
        {
            Sync();
            var second = Sync();
            Assert.IsTrue(second.WasUpToDate);
            Assert.AreEqual(1, second.Unchanged);
        }

        [Test]
        public void A_forced_sync_rewrites_without_duplicating_anything()
        {
            Sync();
            var report = Sync(force: true);
            Assert.IsFalse(report.WasUpToDate);
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:DialogueAsset", new[] { _settings.DialoguesFolder }).Length);
            using (var file = SampleBuild.Open())
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(SampleBuild.Gate(file.Manifest).Guid));
                Assert.AreEqual(6, collection.SharedData.Entries.Count);
            }
        }

        [Test]
        public void A_dialogue_that_is_gone_from_the_build_loses_its_asset_and_its_tables()
        {
            Sync();
            var target = new UnitySyncTarget(_settings);
            Guid id;
            using (var file = SampleBuild.Open()) id = SampleBuild.Gate(file.Manifest).Guid;
            target.RemoveDialogue(id);
            AssetDatabase.Refresh();
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DialogueAsset>(_settings.DialoguePath(id)));
            Assert.IsNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(id)));
            Assert.IsEmpty(target.ExistingDialogues());
        }

        [Test]
        public void Deleted_tables_make_the_dialogue_count_as_missing_so_the_next_sync_makes_it_again()
        {
            Sync();
            Guid id;
            using (var file = SampleBuild.Open()) id = SampleBuild.Gate(file.Manifest).Guid;
            var collection = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(id));
            foreach (var table in collection.StringTables) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(table));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(collection.SharedData));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(collection));
            AssetDatabase.Refresh();

            var report = Sync();
            Assert.AreEqual(1, report.Added);
            Assert.IsNotNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(id)));
        }
    }
}
