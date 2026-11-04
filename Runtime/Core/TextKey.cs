using System;

namespace Loreman.Dialogues
{
    /// <summary>
    /// The number of a text in a string table (the key id Loreman gave it). <see cref="None"/> means that there is no text:
    /// an empty line, an answer without words on its arrow.
    /// </summary>
    public readonly struct TextKey : IEquatable<TextKey>
    {
        public static readonly TextKey None = new TextKey(0);

        public TextKey(long id) { Id = id; }

        public long Id { get; }

        public bool IsNone => Id == 0;

        public bool Equals(TextKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TextKey other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => IsNone ? "none" : Id.ToString();

        public static bool operator ==(TextKey a, TextKey b) => a.Id == b.Id;
        public static bool operator !=(TextKey a, TextKey b) => a.Id != b.Id;
    }
}
