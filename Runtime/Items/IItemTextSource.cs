using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loreman.Dialogues;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Loreman.Items
{
    /// <summary>Where the words of the items come from. <see cref="LocalizationItemTextSource"/> reads Unity Localization; a test can put its own.</summary>
    public interface IItemTextSource : IDisposable
    {
        /// <summary>Loads the item tables of these catalogs in the current language.</summary>
        Task InitializeAsync(IReadOnlyList<ItemCatalog> catalogs, CancellationToken cancellationToken);

        /// <summary>The words in the current language (the language of the project where it is not translated). An empty string for no text.</summary>
        string GetText(Guid projectId, TextKey key);

        /// <summary>The player changed the language and the tables were loaded again: what was read before is out of date.</summary>
        event Action TextsChanged;
    }

    /// <summary>
    /// Reads the words of the items from Unity Localization: one string table for each project (see <see cref="LoremanNames.ItemsTable"/>), loaded at the start (the items are
    /// listed at once) in the current language and in the language of the project, which fills what is not translated. When the player changes the language the tables are loaded again.
    /// </summary>
    public sealed class LocalizationItemTextSource : IItemTextSource
    {
        private sealed class Tables
        {
            public string BaseLanguage;
            public Locale Locale;
            public Locale BaseLocale;
            public StringTable Current;
            public StringTable Base;
        }

        private readonly Dictionary<Guid, Tables> _projects = new Dictionary<Guid, Tables>();
        private bool _subscribed;
        private bool _disposed;

        public event Action TextsChanged;

        public async Task InitializeAsync(IReadOnlyList<ItemCatalog> catalogs, CancellationToken cancellationToken)
        {
            await LocalizationSettings.InitializationOperation.Task;
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var catalog in catalogs)
            {
                var tables = new Tables { BaseLanguage = catalog.BaseLanguage };
                _projects[catalog.ProjectGuid] = tables;
                await LoadAsync(LoremanNames.ItemsTable(catalog.ProjectGuid), tables, cancellationToken);
            }
            if (!_subscribed)
            {
                _subscribed = true;
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
        }

        public string GetText(Guid projectId, TextKey key)
        {
            if (key.IsNone || !_projects.TryGetValue(projectId, out var tables)) return string.Empty;
            var text = tables.Current?.GetEntry(key.Id)?.GetLocalizedString();
            if (!string.IsNullOrEmpty(text)) return text;
            return tables.Base?.GetEntry(key.Id)?.GetLocalizedString() ?? string.Empty;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_subscribed) LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            foreach (var pair in _projects) Unload(LoremanNames.ItemsTable(pair.Key), pair.Value);
            _projects.Clear();
        }

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

        private async void OnLocaleChanged(Locale locale)
        {
            try
            {
                foreach (var pair in new List<KeyValuePair<Guid, Tables>>(_projects))
                {
                    var old = new Tables { Locale = pair.Value.Locale, BaseLocale = pair.Value.BaseLocale };
                    await LoadAsync(LoremanNames.ItemsTable(pair.Key), pair.Value, System.Threading.CancellationToken.None);
                    if (old.Locale != pair.Value.Locale) Unload(LoremanNames.ItemsTable(pair.Key), old); // the new tables are there first: the words never disappear in between
                }
                if (!_disposed) TextsChanged?.Invoke();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
