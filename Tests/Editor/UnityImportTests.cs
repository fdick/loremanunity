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
            // These tests write string tables with the fixed names the package uses (LoremanSpeakers…). In a project that already has real Loreman data
            // they would rewrite it, so they run only in a project that has none (the package's own test project, or a scratch one).
            var existing = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(LoremanNames.SpeakersTable(ProjectOf(SampleBuild.Open)));
            if (existing != null && !AssetDatabase.GetAssetPath(existing).StartsWith(Folder))
                Assert.Ignore("This project already has Loreman data (" + AssetDatabase.GetAssetPath(existing) + "); these tests would overwrite it.");
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
            // Only what is inside the test folder: with no folder, FindAssets would look through the whole project.
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

        private static Guid ProjectOf(Func<LoreBuildFile> open)
        {
            using (var file = open()) return Guid.Parse(file.Manifest.projectId);
        }

        private Guid SampleProject => ProjectOf(SampleBuild.Open);

        private UnitySyncTarget Target() => new UnitySyncTarget(_settings, SampleProject);

        private SyncReport Sync(bool force = false)
        {
            using (var file = SampleBuild.Open())
                return BuildSynchronizer.Run(file, Target(), force);
        }

        private SyncReport SyncOther()
        {
            using (var file = SampleBuild.OpenOther())
                return BuildSynchronizer.Run(file, new UnitySyncTarget(_settings, Guid.Parse(file.Manifest.projectId)));
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
                catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(_settings.CatalogPath(SampleProject));
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
            var catalogEntry = aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(_settings.CatalogPath(SampleProject)));
            Assert.AreEqual(LoremanNames.CatalogAddress(SampleProject), catalogEntry.address);
            Assert.IsTrue(catalogEntry.labels.Contains(LoremanNames.CatalogLabel), "the game finds the catalogs by this label");
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

                var speakers = LocalizationEditorSettings.GetStringTableCollection(LoremanNames.SpeakersTable(SampleProject));
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
            Target().RemoveDialogue(id);
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
            var target = Target();
            Guid id;
            using (var file = SampleBuild.Open()) id = SampleBuild.Gate(file.Manifest).Guid;
            target.RemoveDialogue(id);
            AssetDatabase.Refresh();
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DialogueAsset>(_settings.DialoguePath(id)));
            Assert.IsNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(id)));
            Assert.IsEmpty(target.ExistingDialogues());
        }

        [Test]
        public void A_dialogue_or_catalog_that_lost_its_addressables_entry_is_registered_again_by_the_next_sync()
        {
            Sync();
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
            aa.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(_settings.CatalogPath(SampleProject)), false);
            using (var file = SampleBuild.Open())
                aa.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(_settings.DialoguePath(SampleBuild.Gate(file.Manifest).Guid)), false);

            var report = Sync();
            Assert.IsFalse(report.WasUpToDate);
            Assert.AreEqual(1, report.Added); // the dialogue counted as missing
            Assert.AreEqual(LoremanNames.CatalogAddress(SampleProject), aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(_settings.CatalogPath(SampleProject))).address);
            Assert.IsTrue(Sync().WasUpToDate);
        }

        [Test]
        public void Builds_of_different_projects_live_side_by_side_and_do_not_wipe_each_other()
        {
            Sync();
            var other = SyncOther();
            Assert.AreEqual(1, other.Added);
            Assert.AreEqual(0, other.Removed, "the other build's dialogue must not remove the first build's");

            Guid sampleDialogue, otherDialogue;
            using (var file = SampleBuild.Open()) sampleDialogue = SampleBuild.Gate(file.Manifest).Guid;
            using (var file = SampleBuild.OpenOther()) otherDialogue = file.Manifest.dialogues.Single().Guid;
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<DialogueAsset>(_settings.DialoguePath(sampleDialogue)));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<DialogueAsset>(_settings.DialoguePath(otherDialogue)));

            // two catalogs, each with its own dialogue and its own speakers table
            Assert.AreEqual(2, AssetDatabase.FindAssets("t:DialogueCatalog", new[] { _settings.CatalogsFolder }).Length);
            Assert.AreEqual(sampleDialogue, Target().ExistingDialogues().Keys.Single());
            Assert.IsNotNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.SpeakersTable(SampleProject)));
            using (var file = SampleBuild.OpenOther())
                Assert.IsNotNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.SpeakersTable(Guid.Parse(file.Manifest.projectId))));

            // and each is up to date on its own
            Assert.IsTrue(Sync().WasUpToDate);
            Assert.IsTrue(SyncOther().WasUpToDate);
        }

        [Test]
        public void The_catalog_and_speakers_of_an_older_version_are_removed_and_the_dialogues_adopted_by_their_build()
        {
            Sync();
            // an older version kept one catalog and one speakers table for everything
            var legacy = ScriptableObject.CreateInstance<DialogueCatalog>();
            AssetDatabase.CreateAsset(legacy, _settings.LegacyCatalogPath);
            var legacyCollection = LocalizationEditorSettings.CreateStringTableCollection(LoremanNames.LegacySpeakersTable, _settings.TablesFolder, LocalizationEditorSettings.GetLocales());
            Assert.IsNotNull(legacyCollection);

            var report = Sync(force: true);
            Assert.IsFalse(report.WasUpToDate);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DialogueCatalog>(_settings.LegacyCatalogPath));
            Assert.IsNull(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.LegacySpeakersTable));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<DialogueCatalog>(_settings.CatalogPath(SampleProject)));
        }

        [Test]
        public void Voice_overs_are_imported_as_mono_vorbis_game_audio_and_a_short_one_is_not_streamed()
        {
            Sync();
            using (var file = SampleBuild.Open())
            {
                var gate = SampleBuild.Gate(file.Manifest);
                var line = SampleBuild.GateGraph(file).Nodes[0];
                var path = _settings.AudioFolder(gate.Guid) + "/en/" + line.Id.ToString("N") + ".wav";
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer);
                Assert.IsTrue(importer.forceToMono);
                Assert.IsFalse(importer.defaultSampleSettings.preloadAudioData);
                Assert.IsTrue(importer.loadInBackground);
                Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat);
                Assert.AreEqual(0.5f, importer.defaultSampleSettings.quality, 0.001f);
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, importer.defaultSampleSettings.loadType);
                Assert.AreEqual(VoiceOverImportSettings.Marker, importer.userData);
            }
        }

        [Test]
        public void A_long_recording_is_streamed_and_a_short_one_is_kept_in_memory()
        {
            // the rule, without importing a big file: bigger than the limit streams, smaller does not
            var big = ScriptableObject.CreateInstance<LoremanSettings>();
            try
            {
                big.streamLargerThanKB = 100;
                Assert.AreEqual(AudioClipLoadType.Streaming, VoiceOverImportSettings.LoadTypeFor(big, 101 * 1024L));
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, VoiceOverImportSettings.LoadTypeFor(big, 100 * 1024L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(big);
            }
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
