using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Loreman.Dialogues
{
    /// <summary>
    /// Reads the voice-overs from Unity Localization asset tables: one table per dialogue (see <see cref="LoremanNames.AudioTable"/>), a clip for each language.
    /// Loading the table does not load the clips; each clip is loaded when it is asked for and let go with the dialogue.
    /// </summary>
    public sealed class LocalizationAudioSource : IDialogueAudioSource
    {
        private sealed class Usage
        {
            public int Users;
            public readonly HashSet<Locale> Locales = new HashSet<Locale>();
            public readonly List<AsyncOperationHandle<AudioClip>> Clips = new List<AsyncOperationHandle<AudioClip>>();
        }

        private readonly Dictionary<Guid, Usage> _usage = new Dictionary<Guid, Usage>();
        private bool _disposed;

        public async Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken)
        {
            await LocalizationSettings.InitializationOperation.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (!_usage.TryGetValue(dialogueId, out var usage))
            {
                usage = new Usage();
                _usage[dialogueId] = usage;
            }
            usage.Users++;
        }

        public void Release(Guid dialogueId)
        {
            if (!_usage.TryGetValue(dialogueId, out var usage)) return;
            if (--usage.Users > 0) return;
            _usage.Remove(dialogueId);
            Unload(dialogueId, usage);
        }

        public async Task<AudioClip> LoadClipAsync(Guid dialogueId, TextKey key, CancellationToken cancellationToken)
        {
            if (key.IsNone || !_usage.TryGetValue(dialogueId, out var usage)) return null;

            var locale = LocalizationSettings.SelectedLocale;
            var name = LoremanNames.AudioTable(dialogueId);
            var database = LocalizationSettings.AssetDatabase;

            var table = await database.GetTableAsync(name, locale).Task;
            usage.Locales.Add(locale);
            cancellationToken.ThrowIfCancellationRequested();
            if (table == null || table.GetEntry(key.Id) == null) return null; // nothing recorded in this language

            var handle = database.GetLocalizedAssetAsync<AudioClip>(name, key.Id, locale, FallbackBehavior.DontUseFallback);
            var clip = await handle.Task;
            if (!_usage.ContainsKey(dialogueId) || cancellationToken.IsCancellationRequested)
            {
                // the dialogue was released while the clip was loading
                if (handle.IsValid()) Addressables.Release(handle);
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }
            usage.Clips.Add(handle);
            return clip;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var pair in _usage) Unload(pair.Key, pair.Value);
            _usage.Clear();
        }

        private static void Unload(Guid dialogueId, Usage usage)
        {
            foreach (var handle in usage.Clips)
                if (handle.IsValid()) Addressables.Release(handle);
            usage.Clips.Clear();
            foreach (var locale in usage.Locales) LocalizationSettings.AssetDatabase.ReleaseTable(LoremanNames.AudioTable(dialogueId), locale);
            usage.Locales.Clear();
        }
    }
}
