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
    /// loads only its words, in the current language (and in the base language of the project when something is missing in it). When the player changes the
    /// language, the tables of the dialogues that are open are loaded again and <see cref="TextsChanged"/> is raised.
    /// </summary>
    public sealed class LocalizationTextSource : IDialogueTextSource
    {
        /// <summary>The tables of one dialogue: the one in the current language and the one in the base language, which fills what is missing.</summary>
        private sealed class Tables
        {
            public int Users;
            public Locale Locale;
            public Locale BaseLocale;
            public StringTable Current;
            public StringTable Base;
        }

        private readonly Dictionary<Guid, Tables> _dialogues = new Dictionary<Guid, Tables>();
        private Locale _baseLocale;
        private StringTable _speakers;
        private StringTable _speakersBase;
        private bool _disposed;

        public event Action TextsChanged;

        public async Task InitializeAsync(DialogueCatalog catalog, CancellationToken cancellationToken)
        {
            await LocalizationSettings.InitializationOperation.Task;
            cancellationToken.ThrowIfCancellationRequested();

            _baseLocale = FindLocale(catalog.BaseLanguage);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            await LoadSpeakersAsync(cancellationToken);
        }

        public async Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken)
        {
            if (!_dialogues.TryGetValue(dialogueId, out var tables))
            {
                tables = new Tables();
                _dialogues[dialogueId] = tables;
            }
            tables.Users++;
            try
            {
                if (tables.Current == null) await LoadAsync(dialogueId, tables, cancellationToken);
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
            Unload(LoremanNames.DialogueTable(dialogueId), tables.Locale, tables.BaseLocale);
        }

        public string GetText(Guid dialogueId, TextKey key)
        {
            if (key.IsNone || !_dialogues.TryGetValue(dialogueId, out var tables)) return string.Empty;
            return Read(tables.Current, tables.Base, key);
        }

        public string GetSpeakerText(TextKey key) => key.IsNone ? string.Empty : Read(_speakers, _speakersBase, key);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            foreach (var pair in _dialogues) Unload(LoremanNames.DialogueTable(pair.Key), pair.Value.Locale, pair.Value.BaseLocale);
            _dialogues.Clear();
            Unload(LoremanNames.SpeakersTable, _speakers == null ? null : LocaleOf(_speakers), _speakersBase == null ? null : LocaleOf(_speakersBase));
            _speakers = null;
            _speakersBase = null;
        }

        // ── reading ──

        private static string Read(StringTable current, StringTable fallback, TextKey key)
        {
            var text = current?.GetEntry(key.Id)?.GetLocalizedString();
            if (!string.IsNullOrEmpty(text)) return text;
            // not translated yet: the base language of the project says it, better than nothing
            return fallback?.GetEntry(key.Id)?.GetLocalizedString() ?? string.Empty;
        }

        // ── loading ──

        private async Task LoadAsync(Guid dialogueId, Tables tables, CancellationToken cancellationToken)
        {
            var name = LoremanNames.DialogueTable(dialogueId);
            var locale = LocalizationSettings.SelectedLocale;
            var baseLocale = _baseLocale != null && _baseLocale != locale ? _baseLocale : null;

            var current = await LocalizationSettings.StringDatabase.GetTableAsync(name, locale).Task;
            var fallback = baseLocale == null ? null : await LocalizationSettings.StringDatabase.GetTableAsync(name, baseLocale).Task;
            cancellationToken.ThrowIfCancellationRequested();

            tables.Locale = locale;
            tables.BaseLocale = baseLocale;
            tables.Current = current;
            tables.Base = fallback;
        }

        private async Task LoadSpeakersAsync(CancellationToken cancellationToken)
        {
            var locale = LocalizationSettings.SelectedLocale;
            var baseLocale = _baseLocale != null && _baseLocale != locale ? _baseLocale : null;
            var current = await LocalizationSettings.StringDatabase.GetTableAsync(LoremanNames.SpeakersTable, locale).Task;
            var fallback = baseLocale == null ? null : await LocalizationSettings.StringDatabase.GetTableAsync(LoremanNames.SpeakersTable, baseLocale).Task;
            cancellationToken.ThrowIfCancellationRequested();
            _speakers = current;
            _speakersBase = fallback;
        }

        private static void Unload(string table, Locale locale, Locale baseLocale)
        {
            if (locale != null) LocalizationSettings.StringDatabase.ReleaseTable(table, locale);
            if (baseLocale != null) LocalizationSettings.StringDatabase.ReleaseTable(table, baseLocale);
        }

        private static Locale LocaleOf(StringTable table) => LocalizationSettings.AvailableLocales.GetLocale(table.LocaleIdentifier);

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
                var open = new List<KeyValuePair<Guid, Tables>>(_dialogues);
                foreach (var pair in open)
                {
                    var tables = pair.Value;
                    var oldLocale = tables.Locale;
                    var oldBase = tables.BaseLocale;
                    await LoadAsync(pair.Key, tables, CancellationToken.None);
                    if (oldLocale != tables.Locale) Unload(LoremanNames.DialogueTable(pair.Key), oldLocale, oldBase);
                }
                var oldSpeakers = _speakers == null ? null : LocaleOf(_speakers);
                var oldSpeakersBase = _speakersBase == null ? null : LocaleOf(_speakersBase);
                await LoadSpeakersAsync(CancellationToken.None);
                if (oldSpeakers != locale) Unload(LoremanNames.SpeakersTable, oldSpeakers, oldSpeakersBase);
                if (!_disposed) TextsChanged?.Invoke();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
