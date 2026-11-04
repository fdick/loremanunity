using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// Writes what a sync reads into the project: <see cref="DialogueAsset"/>s and the <see cref="DialogueCatalog"/> as Addressables, and the texts as Unity Localization
    /// string tables (one per dialogue, so the game loads only the words of the dialogue it plays).
    /// </summary>
    public sealed class UnitySyncTarget : ISyncTarget
    {
        private readonly LoremanSettings _settings;
        private readonly IReadOnlyDictionary<string, string> _localeMap;
        private List<Locale> _locales = new List<Locale>();
        private Dictionary<string, Locale> _byLanguage = new Dictionary<string, Locale>(StringComparer.OrdinalIgnoreCase);

        public UnitySyncTarget(LoremanSettings settings)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _localeMap = settings.LocaleMap();
        }

        public string ContentHash
        {
            get
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(_settings.CatalogPath);
                return catalog == null ? string.Empty : catalog.ContentHash ?? string.Empty;
            }
        }

        public IReadOnlyDictionary<Guid, string> ExistingDialogues()
        {
            var result = new Dictionary<Guid, string>();
            if (!AssetDatabase.IsValidFolder(_settings.DialoguesFolder)) return result;
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueAsset", new[] { _settings.DialoguesFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset.Id == Guid.Empty) continue;
                // an asset whose texts were deleted is as good as missing: the sync makes both again
                if (LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(asset.Id)) == null) continue;
                result[asset.Id] = asset.Hash;
            }
            return result;
        }

        public void Begin()
        {
            LoremanSettings.EnsureFolder(_settings.outputFolder.TrimEnd('/'));
            LoremanSettings.EnsureFolder(_settings.DialoguesFolder);
            LoremanSettings.EnsureFolder(_settings.TablesFolder);
        }

        public void End()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── locales ──

        public void EnsureLocales(IReadOnlyList<string> languages)
        {
            _byLanguage = new Dictionary<string, Locale>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in languages)
            {
                var code = LocaleCodes.ToUnity(language, _localeMap);
                var locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => string.Equals(l.Identifier.Code, code, StringComparison.OrdinalIgnoreCase));
                if (locale == null)
                {
                    if (!_settings.createMissingLocales)
                        throw new InvalidOperationException($"Loreman: the project has no Unity locale \"{code}\" for the language \"{language}\". Add it in Localization Settings or turn on Create Missing Locales.");
                    locale = CreateLocale(code);
                }
                _byLanguage[language] = locale;
            }
            _locales = _byLanguage.Values.Distinct().ToList();
        }

        private Locale CreateLocale(string code)
        {
            LoremanSettings.EnsureFolder(_settings.LocalesFolder);
            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            AssetDatabase.CreateAsset(locale, _settings.LocalesFolder + "/" + code + ".asset");
            LocalizationEditorSettings.AddLocale(locale);
            Debug.Log($"Loreman: made the Unity locale \"{code}\".");
            return locale;
        }

        // ── dialogues ──

        public void WriteDialogue(ManifestDialogue dialogue, byte[] graph, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts, IReadOnlyList<SyncAudioFile> audio)
        {
            var id = dialogue.Guid;
            var path = _settings.DialoguePath(id);
            var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
            var isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<DialogueAsset>();
            asset.Populate(id, dialogue.hash, graph);
            if (isNew) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);

            RegisterAddressable(path, LoremanNames.DialogueAddress(id), _settings.packDialoguesSeparately);
            FillCollection(LoremanNames.DialogueTable(id), texts);
            WriteAudio(id, audio);
        }

        public void RemoveDialogue(Guid dialogueId)
        {
            var path = _settings.DialoguePath(dialogueId);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (aa != null && !string.IsNullOrEmpty(guid)) aa.RemoveAssetEntry(guid, false);
            AssetDatabase.DeleteAsset(path);

            DeleteCollection(LocalizationEditorSettings.GetStringTableCollection(LoremanNames.DialogueTable(dialogueId)));
            RemoveAudio(dialogueId);
        }

        /// <summary>Deletes a table collection with its tables and its shared data.</summary>
        private static void DeleteCollection(LocalizationTableCollection collection)
        {
            if (collection == null) return;
            var paths = new List<string>();
            foreach (var table in collection.Tables) paths.Add(AssetDatabase.GetAssetPath(table.asset));
            paths.Add(AssetDatabase.GetAssetPath(collection.SharedData));
            paths.Add(AssetDatabase.GetAssetPath(collection));
            foreach (var p in paths)
                if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p);
        }

        // ── voice-overs ──

        /// <summary>
        /// Puts the voice-overs of a dialogue into the project: each recording is a file under the Audio folder (Unity imports it as an AudioClip) and an entry
        /// of the asset table of the dialogue, in the locale of its language. What the dialogue had before goes first, so a recording that is gone does not stay.
        /// </summary>
        private void WriteAudio(Guid dialogueId, IReadOnlyList<SyncAudioFile> audio)
        {
            RemoveAudio(dialogueId);
            if (audio.Count == 0) return;

            var folder = _settings.AudioFolder(dialogueId);
            LoremanSettings.EnsureFolder(folder);
            var collection = LocalizationEditorSettings.CreateAssetTableCollection(LoremanNames.AudioTable(dialogueId), _settings.TablesFolder, _locales);
            var shared = collection.SharedData;

            foreach (var a in audio)
            {
                if (!_byLanguage.TryGetValue(a.Language, out var locale))
                {
                    Debug.LogWarning("Loreman: a voice-over is in the language \"" + a.Language + "\", which the build does not list. Skipped.");
                    continue;
                }
                var languageFolder = folder + "/" + a.Language;
                LoremanSettings.EnsureFolder(languageFolder);
                var assetPath = languageFolder + "/" + a.Id.ToString("N") + "." + a.Extension;
                System.IO.File.WriteAllBytes(assetPath, a.Read());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null)
                {
                    Debug.LogWarning("Loreman: Unity could not read the voice-over \"" + assetPath + "\" as an audio clip.");
                    continue;
                }
                if (!shared.Contains(a.Key)) shared.AddKey("audio:" + a.Id.ToString("D"), a.Key);
                collection.AddAssetToTable(locale.Identifier, a.Key, clip);
            }
            EditorUtility.SetDirty(shared);
            EditorUtility.SetDirty(collection);
        }

        private void RemoveAudio(Guid dialogueId)
        {
            DeleteCollection(LocalizationEditorSettings.GetAssetTableCollection(LoremanNames.AudioTable(dialogueId)));
            var folder = _settings.AudioFolder(dialogueId);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }

        public void WriteSpeakers(IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts) => FillCollection(LoremanNames.SpeakersTable, texts);

        public void WriteCatalog(BuildManifest manifest)
        {
            var path = _settings.CatalogPath;
            var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(path);
            var isNew = catalog == null;
            if (isNew) catalog = ScriptableObject.CreateInstance<DialogueCatalog>();

            var languages = manifest.languages.Select(l => l.code).ToList();
            if (!languages.Contains(manifest.baseLanguage)) languages.Insert(0, manifest.baseLanguage);
            catalog.Populate(
                manifest.projectName, manifest.contentHash, manifest.baseLanguage, languages, manifest.speakers, manifest.groups,
                manifest.dialogues.Select(d => new CatalogDialogue { id = d.id, name = d.name, groupId = d.groupId, hash = d.hash, nodes = d.nodes }));

            if (isNew) AssetDatabase.CreateAsset(catalog, path);
            else EditorUtility.SetDirty(catalog);
            RegisterAddressable(path, LoremanNames.CatalogAddress, null);
        }

        // ── Unity Localization ──

        /// <summary>
        /// Makes the table collection hold exactly these texts: keys that are not wanted any more go, the others are added or changed, and a language that has
        /// no text for a key gets none (so the game falls back to the base language).
        /// </summary>
        private void FillCollection(string name, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> textsByLanguage)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(name)
                             ?? LocalizationEditorSettings.CreateStringTableCollection(name, _settings.TablesFolder, _locales);

            // the keys the collection must have: one per text, with the name Loreman gave it
            var wanted = new Dictionary<long, string>();
            foreach (var list in textsByLanguage.Values)
                foreach (var e in list) wanted[e.Id] = e.Key;

            var shared = collection.SharedData;
            foreach (var stale in shared.Entries.Where(e => !wanted.ContainsKey(e.Id)).Select(e => e.Id).ToList()) collection.RemoveEntry(stale);
            foreach (var pair in wanted)
                if (!shared.Contains(pair.Key)) shared.AddKey(pair.Value, pair.Key);
            EditorUtility.SetDirty(shared);

            foreach (var pair in _byLanguage)
            {
                var table = collection.GetTable(pair.Value.Identifier) as StringTable
                            ?? collection.AddNewTable(pair.Value.Identifier) as StringTable;
                if (table == null) continue;

                var texts = new Dictionary<long, string>();
                if (textsByLanguage.TryGetValue(pair.Key, out var list))
                    foreach (var e in list) texts[e.Id] = e.Text;

                foreach (var key in wanted.Keys)
                {
                    if (texts.TryGetValue(key, out var text)) table.AddEntry(key, text);
                    else if (table.ContainsKey(key)) table.RemoveEntry(key);
                }
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection);
        }

        // ── Addressables ──

        /// <param name="packSeparately">Sets the bundle mode of the group (null leaves it as it is).</param>
        private void RegisterAddressable(string assetPath, string address, bool? packSeparately)
        {
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = aa.FindGroup(_settings.addressablesGroup)
                        ?? aa.CreateGroup(_settings.addressablesGroup, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null && packSeparately.HasValue)
                schema.BundleMode = packSeparately.Value ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately : BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = aa.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            aa.AddLabel(LoremanNames.Label, false);
            entry.SetLabel(LoremanNames.Label, true, false, false);
            aa.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }
}
