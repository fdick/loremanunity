using System;
using UnityEngine;

namespace Loreman.Dialogues
{
    /// <summary>
    /// The graph of one dialogue, as an asset the game loads through Addressables when it needs the dialogue. It holds the binary graph of the build as it is
    /// (no texts: those are in the Unity Localization table of the dialogue) and is parsed once, when the dialogue is opened.
    /// The importer makes these assets; nobody edits them by hand.
    /// </summary>
    public sealed class DialogueAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string hash;
        [SerializeField, HideInInspector] private byte[] graph = Array.Empty<byte>();

        public Guid Id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty;

        /// <summary>The code of the dialogue in the build it came from: the importer remakes the asset only when it changes.</summary>
        public string Hash => hash;

        /// <summary>The bytes of the graph (see <see cref="DialogueGraphReader"/>).</summary>
        public byte[] GraphBytes => graph;

        /// <summary>Fills the asset. Called by the importer.</summary>
        public void Populate(Guid dialogueId, string contentHash, byte[] graphBytes)
        {
            id = dialogueId.ToString("D");
            hash = contentHash;
            graph = graphBytes ?? throw new ArgumentNullException(nameof(graphBytes));
        }
    }
}
