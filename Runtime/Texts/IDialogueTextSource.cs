using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loreman.Dialogues
{
    /// <summary>
    /// Where the words come from. A dialogue prepares its texts once (that is the only waiting), then every line is read at once, without waiting.
    /// <see cref="LocalizationTextSource"/> reads Unity Localization tables; a test or another system can put its own.
    /// </summary>
    public interface IDialogueTextSource : IDisposable
    {
        /// <summary>The language was changed: what was read before is out of date. Read the texts again.</summary>
        event Action TextsChanged;

        /// <summary>Gives the source the catalogs (the base language of each project) and loads the speakers' texts. Called once, before anything else.</summary>
        Task InitializeAsync(IReadOnlyList<DialogueCatalog> catalogs, CancellationToken cancellationToken);

        /// <summary>Loads the texts of a dialogue in the current language. Every call must be matched by <see cref="Release"/>.</summary>
        Task PrepareAsync(Guid dialogueId, CancellationToken cancellationToken);

        /// <summary>The texts of the dialogue are not needed any more; they are unloaded when nobody needs them.</summary>
        void Release(Guid dialogueId);

        /// <summary>A text of a dialogue, ready for the screen. Empty when there is none. The dialogue must be prepared.</summary>
        string GetText(Guid dialogueId, TextKey key);

        /// <summary>A text of the speakers table of a project (a name, a description). Empty when there is none.</summary>
        string GetSpeakerText(Guid projectId, TextKey key);
    }
}
