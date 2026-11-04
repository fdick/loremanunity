using System;

namespace Loreman.Dialogues
{
    /// <summary>
    /// The names the package gives to what it makes: the importer writes assets under them and the game finds them by the same names,
    /// so this is the one place that knows them.
    /// </summary>
    public static class LoremanNames
    {
        /// <summary>The Addressables label of everything the package makes.</summary>
        public const string Label = "loreman";

        /// <summary>The address of the catalog: the small list of dialogues the game loads first.</summary>
        public const string CatalogAddress = "loreman/catalog";

        /// <summary>The Unity Localization table (collection) that holds the names and descriptions of the speakers.</summary>
        public const string SpeakersTable = "LoremanSpeakers";

        /// <summary>The Unity Localization asset table (collection) that holds the voice-overs of a dialogue, one clip per language.</summary>
        public static string AudioTable(Guid dialogueId) => "LoremanAudio_" + dialogueId.ToString("N");

        /// <summary>The address of the graph of a dialogue.</summary>
        public static string DialogueAddress(Guid dialogueId) => "loreman/dialogues/" + dialogueId.ToString("N");

        /// <summary>The Unity Localization table (collection) that holds the texts of a dialogue: one table per dialogue, so a dialogue loads only its own words.</summary>
        public static string DialogueTable(Guid dialogueId) => "LoremanDialogue_" + dialogueId.ToString("N");
    }
}
