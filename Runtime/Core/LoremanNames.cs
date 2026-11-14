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

        /// <summary>The Addressables label of the catalogs. The game loads every catalog that has it: each build of a Loreman project has one, so builds live side by side.</summary>
        public const string CatalogLabel = "loreman.catalog";

        /// <summary>The address of the catalog of a project: the small list of its dialogues the game loads first.</summary>
        public static string CatalogAddress(Guid projectId) => "loreman/catalog/" + projectId.ToString("N");

        /// <summary>The Unity Localization table (collection) that holds the names and descriptions of the speakers of a project.</summary>
        public static string SpeakersTable(Guid projectId) => "LoremanSpeakers_" + projectId.ToString("N");

        /// <summary>The names the first versions used, when one catalog served the whole Unity project. The importer removes them.</summary>
        public const string LegacyCatalogAddress = "loreman/catalog";

        public const string LegacySpeakersTable = "LoremanSpeakers";

        /// <summary>The Unity Localization asset table (collection) that holds the voice-overs of a dialogue, one clip per language.</summary>
        public static string AudioTable(Guid dialogueId) => "LoremanAudio_" + dialogueId.ToString("N");

        /// <summary>The Addressables label of the item catalogs. The game loads every catalog that has it: each build of a Loreman project has one.</summary>
        public const string ItemCatalogLabel = "loreman.items";

        /// <summary>The address of the item catalog of a project: every item of the build (names, numbers, folders) in one small asset.</summary>
        public static string ItemCatalogAddress(Guid projectId) => "loreman/items/" + projectId.ToString("N");

        /// <summary>The Unity Localization table (collection) that holds the names, descriptions and text parameters of the items of a project, one string for each language.</summary>
        public static string ItemsTable(Guid projectId) => "LoremanItems_" + projectId.ToString("N");

        /// <summary>The address of the picture of an item. <paramref name="code"/> is the code in the file name of the picture in the build (<c>items/icons/{code}.png</c>); the project is in the
        /// address because two projects may have the same picture.</summary>
        public static string ItemIconAddress(Guid projectId, string code) => "loreman/items/" + projectId.ToString("N") + "/icons/" + code;

        /// <summary>The address of the graph of a dialogue.</summary>
        public static string DialogueAddress(Guid dialogueId) => "loreman/dialogues/" + dialogueId.ToString("N");

        /// <summary>The Unity Localization table (collection) that holds the texts of a dialogue: one table per dialogue, so a dialogue loads only its own words.</summary>
        public static string DialogueTable(Guid dialogueId) => "LoremanDialogue_" + dialogueId.ToString("N");
    }
}
