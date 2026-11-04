using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Loreman.Dialogues
{
    /// <summary>One text of a string table of a build.</summary>
    public readonly struct StringTableEntry
    {
        public StringTableEntry(long id, string key, string text)
        {
            Id = id;
            Key = key;
            Text = text;
        }

        /// <summary>The number of the text: the key id in the Unity string table.</summary>
        public long Id { get; }

        /// <summary>The name of the key (<c>node:…</c>), kept so a table in the editor is readable.</summary>
        public string Key { get; }

        public string Text { get; }
    }

    /// <summary>Reads <c>texts/{language}/{id}.bin</c> of a build (the importer uses it to fill the Unity Localization tables).</summary>
    public static class StringTableReader
    {
        public const byte SupportedVersion = 1;

        private static readonly byte[] Magic = { (byte)'L', (byte)'S', (byte)'T', (byte)'R' };

        public static IReadOnlyList<StringTableEntry> Read(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var r = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (!BinaryFormat.ReadMagic(r, Magic)) throw new DialogueFormatException("This is not a table of texts.");
                    var version = r.ReadByte();
                    if (version != SupportedVersion) throw new DialogueFormatException($"The table of texts is of version {version}; this package reads version {SupportedVersion}.");
                    var count = BinaryFormat.ReadVarInt(r);
                    if (count < 0 || count > data.Length) throw new DialogueFormatException("The table of texts says it has more entries than it can hold.");
                    var list = new List<StringTableEntry>(count);
                    for (var i = 0; i < count; i++) list.Add(new StringTableEntry(r.ReadInt64(), r.ReadString(), r.ReadString()));
                    if (stream.Position != stream.Length) throw new DialogueFormatException("The table of texts has bytes left over.");
                    return list;
                }
            }
            catch (EndOfStreamException e)
            {
                throw new DialogueFormatException("The table of texts ends too early.", e);
            }
        }
    }
}
