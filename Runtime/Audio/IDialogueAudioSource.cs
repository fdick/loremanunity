using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Loreman.Dialogues
{
    /// <summary>
    /// Where the voice-overs come from. A clip is loaded when somebody asks for it (the line is about to be said), not with the dialogue:
    /// a dialogue may have hundreds of recordings in several languages and the game needs one at a time.
    /// <see cref="LocalizationAudioSource"/> reads Unity Localization asset tables.
    /// </summary>
    public interface IDialogueAudioSource : IDisposable
    {
        /// <summary>The dialogue is going to be played: get ready to give its clips. Every call must be matched by <see cref="Release"/>.</summary>
        Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken);

        /// <summary>The dialogue is over: every clip loaded for it is let go.</summary>
        void Release(Guid dialogueId);

        /// <summary>
        /// The recording of a line or an answer in the current language, or null when there is none in this language (the package does not
        /// fall back to another language: a translated line read in the original voice is wrong more often than it is useful).
        /// The clip stays loaded until the dialogue is released.
        /// </summary>
        Task<AudioClip> LoadClipAsync(Guid dialogueId, TextKey key, CancellationToken cancellationToken);
    }

    /// <summary>No voice-overs at all (a game without them, or a test).</summary>
    public sealed class NullAudioSource : IDialogueAudioSource
    {
        public Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Release(Guid dialogueId) { }

        public Task<AudioClip> LoadClipAsync(Guid dialogueId, TextKey key, CancellationToken cancellationToken) => Task.FromResult<AudioClip>(null);

        public void Dispose() { }
    }
}
