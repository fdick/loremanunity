using System;

namespace Loreman.Dialogues
{
    /// <summary>The data of a build cannot be read: it is damaged, or it was made by a newer version of Loreman.</summary>
    public sealed class DialogueFormatException : Exception
    {
        public DialogueFormatException(string message) : base(message) { }

        public DialogueFormatException(string message, Exception inner) : base(message, inner) { }
    }
}
