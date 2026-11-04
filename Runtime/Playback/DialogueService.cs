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
        private bool _initialized;

        public DialogueService(IAssetLoader loader, IDialogueTextSource texts)
        {
            _library = new DialogueLibrary(loader ?? throw new ArgumentNullException(nameof(loader)));
            _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        }

        /// <summary>A service that loads with Addressables and reads texts from Unity Localization.</summary>
        public static DialogueService CreateDefault() => new DialogueService(new AddressablesAssetLoader(), new LocalizationTextSource());

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
            var lease = await _library.AcquireAsync(dialogueId, cancellationToken);
            try
            {
                await _texts.PrepareAsync(dialogueId, cancellationToken);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
            return new DialogueConversation(lease, _library.Catalog, _texts);
        }

        /// <summary>
        /// Loads a dialogue ahead of time (while the player walks to the character, say), so <see cref="StartAsync"/> does not wait.
        /// The dialogue stays in memory until the returned handle is disposed.
        /// </summary>
        public async Task<IDisposable> PreloadAsync(Guid dialogueId, CancellationToken cancellationToken = default)
        {
            if (!_initialized) throw new InvalidOperationException("Loreman: await InitializeAsync before preloading a dialogue.");
            var lease = await _library.AcquireAsync(dialogueId, cancellationToken);
            try
            {
                await _texts.PrepareAsync(dialogueId, cancellationToken);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
            return new Preload(lease, _texts);
        }

        public void Dispose()
        {
            _texts.Dispose();
            _library.Dispose();
        }

        private sealed class Preload : IDisposable
        {
            private readonly DialogueLease _lease;
            private readonly IDialogueTextSource _texts;
            private bool _disposed;

            public Preload(DialogueLease lease, IDialogueTextSource texts)
            {
                _lease = lease;
                _texts = texts;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _texts.Release(_lease.Id);
                _lease.Dispose();
            }
        }
    }
}
