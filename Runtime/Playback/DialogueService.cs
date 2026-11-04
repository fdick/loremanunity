using System;
using System.Threading;
using System.Threading.Tasks;

namespace Loreman.Dialogues
{
    /// <summary>
    /// The door of the package for the game. Await <see cref="InitializeAsync"/> once, then <see cref="StartAsync"/> a dialogue whenever it is needed:
    /// only that dialogue's graph and texts are loaded, and they go when the conversation is disposed.
    /// <code>
    /// var dialogues = DialogueService.CreateDefault();
    /// await dialogues.InitializeAsync();
    /// using var talk = await dialogues.StartAsync(dialogueId);
    /// talk.Presented += view => Show(view);
    /// talk.Start();
    /// </code>
    /// </summary>
    public sealed class DialogueService : IDisposable
    {
        private readonly DialogueLibrary _library;
        private readonly IDialogueTextSource _texts;
        private readonly IDialogueAudioSource _audio;
        private bool _initialized;

        /// <param name="audio">Where the voice-overs come from (null: the dialogues are played without voice).</param>
        public DialogueService(IAssetLoader loader, IDialogueTextSource texts, IDialogueAudioSource audio = null)
        {
            _library = new DialogueLibrary(loader ?? throw new ArgumentNullException(nameof(loader)));
            _texts = texts ?? throw new ArgumentNullException(nameof(texts));
            _audio = audio ?? new NullAudioSource();
        }

        /// <summary>A service that loads with Addressables and reads texts and voice-overs from Unity Localization.</summary>
        public static DialogueService CreateDefault() => new DialogueService(new AddressablesAssetLoader(), new LocalizationTextSource(), new LocalizationAudioSource());

        public bool IsInitialized => _initialized;

        /// <summary>What the build holds (null before <see cref="InitializeAsync"/>).</summary>
        public DialogueCatalog Catalog => _library.Catalog;

        /// <summary>How many dialogues are in memory now.</summary>
        public int LoadedCount => _library.LoadedCount;

        /// <summary>Loads the catalog and the texts of the speakers. Call it once, at the start of the game.</summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized) return;
            await _library.InitializeAsync(cancellationToken);
            await _texts.InitializeAsync(_library.Catalog, cancellationToken);
            _initialized = true;
        }

        /// <summary>Loads a dialogue and its texts and gives a conversation to play. Call <see cref="DialogueConversation.Start"/> on it.</summary>
        public async Task<DialogueConversation> StartAsync(Guid dialogueId, CancellationToken cancellationToken = default)
        {
            if (!_initialized) throw new InvalidOperationException("Loreman: await InitializeAsync before starting a dialogue.");
            var (lease, audioPrepared) = await AcquireAsync(dialogueId, cancellationToken);
            return new DialogueConversation(lease, _library.Catalog, _texts, _audio, audioPrepared);
        }

        /// <summary>Loads the graph and gets the texts (and the voice-overs, when the dialogue has any) ready. On a failure nothing stays held.</summary>
        private async Task<(DialogueLease Lease, bool AudioPrepared)> AcquireAsync(Guid dialogueId, CancellationToken cancellationToken)
        {
            var lease = await _library.AcquireAsync(dialogueId, cancellationToken);
            var textsPrepared = false;
            try
            {
                await _texts.PrepareAsync(dialogueId, cancellationToken);
                textsPrepared = true;
                if (!lease.Graph.HasAudio) return (lease, false);
                await _audio.PrepareAsync(dialogueId, cancellationToken);
                return (lease, true);
            }
            catch
            {
                if (textsPrepared) _texts.Release(dialogueId);
                lease.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Loads a dialogue ahead of time (while the player walks to the character, say), so <see cref="StartAsync"/> does not wait.
        /// The dialogue stays in memory until the returned handle is disposed.
        /// </summary>
        public async Task<IDisposable> PreloadAsync(Guid dialogueId, CancellationToken cancellationToken = default)
        {
            if (!_initialized) throw new InvalidOperationException("Loreman: await InitializeAsync before preloading a dialogue.");
            var (lease, audioPrepared) = await AcquireAsync(dialogueId, cancellationToken);
            return new Preload(lease, _texts, audioPrepared ? _audio : null);
        }

        public void Dispose()
        {
            _audio.Dispose();
            _texts.Dispose();
            _library.Dispose();
        }

        private sealed class Preload : IDisposable
        {
            private readonly DialogueLease _lease;
            private readonly IDialogueTextSource _texts;
            private readonly IDialogueAudioSource _audio; // null when the dialogue has no voice-overs
            private bool _disposed;

            public Preload(DialogueLease lease, IDialogueTextSource texts, IDialogueAudioSource audio)
            {
                _lease = lease;
                _texts = texts;
                _audio = audio;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _audio?.Release(_lease.Id);
                _texts.Release(_lease.Id);
                _lease.Dispose();
            }
        }
    }
}
