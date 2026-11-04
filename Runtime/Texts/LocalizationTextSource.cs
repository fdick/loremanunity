using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Loreman.Dialogues
{
    /// <summary>
    /// Reads the texts from Unity Localization. Every dialogue has a string table of its own (see <see cref="LoremanNames.DialogueTable"/>), so opening a dialogue
    /// loads only its words, in the current language (and in the base language of its project when something is missing in it). The speakers of each project
    /// have a table of their own too. When the player changes the language, the tables of the dialogues that are open are loaded again and
    /// <see cref="TextsChanged"/> is raised.
    /// </summary>
    public sealed class LocalizationTextSource : IDialogueTextSource
    {
        /// <summary>The tables of one dialogue (or of the speakers of one project): the one in the current language and the one in the base language, which fills what is missing.</summary>
        private sealed class Tables
        {
            public int Users;
            public string BaseLanguage;
            public Locale Locale;
            public Locale BaseLocale;
            public StringTable Current;
            public StringTable Base;
        }

        private readonly Dictionary<Guid, Tables> _dialogues = new Dictionary<Guid, Tables>();
        private readonly Dictionary<Guid, string> _baseOfDialogue = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, Tables> _speakers = new Dictionary<Guid, Tables>();
        private bool _disposed;

        public event Action TextsChanged;

        public async Task InitializeAsync(IReadOnlyList<DialogueCatalog> catalogs, CancellationToken cancellationToken)
        {
            await LocalizationSettings.InitializationOperation.Task;
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var catalog in catalogs)
            {
                foreach (var dialogue in catalog.Dialogues)
                    if (!_baseOfDialogue.ContainsKey(dialogue.Guid)) _baseOfDialogue[dialogue.Guid] = catalog.BaseLanguage;
                var speakers = new Tables { BaseLanguage = catalog.BaseLanguage };
                _speakers[catalog.ProjectGuid] = speakers;
                await LoadAsync(LoremanNames.SpeakersTable(catalog.ProjectGuid), speakers, cancellationToken);
            }
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        public async Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken)
        {
            if (!_dialogues.TryGetValue(dialogueId, out var tables))
            {
                tables = new Tables { BaseLanguage = _baseOfDialogue.TryGetValue(dialogueId, out var language) ? language : null };
                _dialogues[dialogueId] = tables;
            }
            tables.Users++;
            try
            {
                if (tables.Current == null) await LoadAsync(LoremanNames.DialogueTable(dialogueId), tables, cancellationToken);
            }
            catch
            {
                Release(dialogueId);
                throw;
            }
        }

        public void Release(Guid dialogueId)
        {
            if (!_dialogues.TryGetValue(dialogueId, out var tables)) return;
            if (--tables.Users > 0) return;
            _dialogues.Remove(dialogueId);
            Unload(LoremanNames.DialogueTable(dialogueId), tables);
        }

        public string GetText(Guid dialogueId, TextKey key)
        {
            if (key.IsNone || !_dialogues.TryGetValue(dialogueId, out var tables)) return string.Empty;
            return Read(tables, key);
        }

        public string GetSpeakerText(Guid projectId, TextKey key)
        {
            if (key.IsNone || !_speakers.TryGetValue(projectId, out var tables)) return string.Empty;
            return Read(tables, key);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            foreach (var pair in _dialogues) Unload(LoremanNames.DialogueTable(pair.Key), pair.Value);
            _dialogues.Clear();
            foreach (var pair in _speakers) Unload(LoremanNames.SpeakersTable(pair.Key), pair.Value);
            _speakers.Clear();
        }

        // ── reading ──

        private static string Read(Tables tables, TextKey key)
        {
            var text = tables.Current?.GetEntry(key.Id)?.GetLocalizedString();
            if (!string.IsNullOrEmpty(text)) return text;
            // not translated yet: the base language of the project says it, better than nothing
            return tables.Base?.GetEntry(key.Id)?.GetLocalizedString() ?? string.Empty;
        }

        // ── loading ──

        private static async Task LoadAsync(string name, Tables tables, CancellationToken cancellationToken)
        {
            var locale = LocalizationSettings.SelectedLocale;
            var baseLocale = FindLocale(tables.BaseLanguage);
            if (baseLocale == locale) baseLocale = null;

            var current = await LocalizationSettings.StringDatabase.GetTableAsync(name, locale).Task;
            var fallback = baseLocale == null ? null : await LocalizationSettings.StringDatabase.GetTableAsync(name, baseLocale).Task;
            cancellationToken.ThrowIfCancellationRequested();

            tables.Locale = locale;
            tables.BaseLocale = baseLocale;
            tables.Current = current;
            tables.Base = fallback;
        }

        private static void Unload(string table, Tables tables)
        {
            if (tables.Locale != null) LocalizationSettings.StringDatabase.ReleaseTable(table, tables.Locale);
            if (tables.BaseLocale != null) LocalizationSettings.StringDatabase.ReleaseTable(table, tables.BaseLocale);
        }

        private static Locale FindLocale(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                if (string.Equals(locale.Identifier.Code, code, StringComparison.OrdinalIgnoreCase)) return locale;
            return null;
        }

        // ── the player changed the language ──

        private async void OnLocaleChanged(Locale locale)
        {
            try
            {
                foreach (var pair in new List<KeyValuePair<Guid, Tables>>(_dialogues)) await Reload(LoremanNames.DialogueTable(pair.Key), pair.Value);
                foreach (var pair in new List<KeyValuePair<Guid, Tables>>(_speakers)) await Reload(LoremanNames.SpeakersTable(pair.Key), pair.Value);
                if (!_disposed) TextsChanged?.Invoke();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private static async Task Reload(string name, Tables tables)
        {
            var old = new Tables { Locale = tables.Locale, BaseLocale = tables.BaseLocale };
            await LoadAsync(name, tables, CancellationToken.None);
            if (old.Locale != tables.Locale) Unload(name, old); // the new tables are loaded first, so the words never disappear in between
        }
    }
}
