using System;
using UnityEngine;

namespace Loreman.Items
{
    /// <summary>
    /// Every item of one Loreman project's build, as one small asset the game loads at the start (a game wants the name and picture of every item at hand: an inventory lists them).
    /// It holds the catalog of the build as it is (<c>items/items.bin</c>) and parses it once; the words are in the project's items string table, and a picture is a
    /// separate Addressable that is loaded when somebody asks for it. The importer makes this asset.
    /// </summary>
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private string projectId;
        [SerializeField] private string projectName;
        [SerializeField] private string contentHash;
        [SerializeField] private string baseLanguage;
        [SerializeField, HideInInspector] private byte[] data = Array.Empty<byte>();

        [NonSerialized] private ItemsData _parsed;

        /// <summary>The id of the Loreman project this catalog was made from.</summary>
        public Guid ProjectGuid => Guid.TryParse(projectId, out var guid) ? guid : Guid.Empty;

        public string ProjectName => projectName;

        /// <summary>The code of the items of the build this was made from: the importer remakes the items only when it changes.</summary>
        public string ContentHash => contentHash;

        /// <summary>The language the project is written in: it fills what an item is not translated into.</summary>
        public string BaseLanguage => baseLanguage;

        /// <summary>The items, parsed (once).</summary>
        public ItemsData Data => _parsed ?? (_parsed = ItemsReader.Read(data));

        /// <summary>Fills the asset. Called by the importer.</summary>
        public void Populate(string projectGuid, string project, string hash, string baseCode, byte[] catalog)
        {
            projectId = projectGuid;
            projectName = project;
            contentHash = hash;
            baseLanguage = baseCode;
            data = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _parsed = null;
        }

        private void OnEnable() => _parsed = null;
    }
}
