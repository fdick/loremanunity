using System;
using UnityEngine;

namespace Loreman.Dialogues
{
    /// <summary>
    /// A dialogue picked in the inspector: use it as a field of a component and choose the dialogue from a list (the list is the catalog the importer made).
    /// It keeps only the id of the dialogue in Loreman, which does not change when the dialogue is renamed or moved.
    /// <code>[SerializeField] private DialogueReference greeting;</code>
    /// </summary>
    [Serializable]
    public struct DialogueReference
    {
        [SerializeField] private string id;

        public DialogueReference(Guid dialogueId)
        {
            id = dialogueId == Guid.Empty ? string.Empty : dialogueId.ToString("D");
        }

        public bool IsSet => Guid != Guid.Empty;

        public Guid Guid => Guid.TryParse(id, out var guid) ? guid : Guid.Empty;

        public static implicit operator Guid(DialogueReference reference) => reference.Guid;

        public override string ToString() => IsSet ? id : "(no dialogue)";
    }
}
