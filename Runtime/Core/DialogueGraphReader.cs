using System;
using System.IO;
using System.Text;

namespace Loreman.Dialogues
{
    /// <summary>
    /// Reads the binary graph of one dialogue (<c>dialogues/{id}.bin</c> of a build) into a <see cref="DialogueGraph"/>.
    /// The layout is described in Documentation~/BUILD_FORMAT.md; a reference to a node is an index plus one, so that 0 means "nowhere".
    /// </summary>
    public static class DialogueGraphReader
    {
        public const byte SupportedVersion = 1;

        private static readonly byte[] Magic = { (byte)'L', (byte)'D', (byte)'L', (byte)'G' };

        // A node takes at least 20 bytes; the limit keeps a damaged count from asking for a huge list.
        private const int MinNodeBytes = 20;

        public static DialogueGraph Read(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var graph = ReadGraph(reader, data.Length);
                    if (stream.Position != stream.Length) throw new DialogueFormatException("The dialogue data has bytes left over.");
                    return graph;
                }
            }
            catch (EndOfStreamException e)
            {
                throw new DialogueFormatException("The dialogue data ends too early.", e);
            }
        }

        private static DialogueGraph ReadGraph(BinaryReader r, int length)
        {
            if (!BinaryFormat.ReadMagic(r, Magic)) throw new DialogueFormatException("This is not the data of a dialogue.");
            var version = r.ReadByte();
            if (version != SupportedVersion)
                throw new DialogueFormatException($"The dialogue data is of version {version}; this package reads version {SupportedVersion}. Update the package or build the project again.");

            var id = BinaryFormat.ReadGuid(r);
            var entryRef = BinaryFormat.ReadVarInt(r);
            var count = BinaryFormat.ReadVarInt(r);
            if (count < 0 || count > length / MinNodeBytes) throw new DialogueFormatException("The dialogue data says it has more nodes than it can hold.");
            if (entryRef < 0 || entryRef > count) throw new DialogueFormatException("The start of the dialogue points at a node that is not there.");

            var nodes = new DialogueNode[count];
            for (var i = 0; i < count; i++) nodes[i] = ReadNode(r, i, count);
            return new DialogueGraph(id, entryRef - 1, nodes);
        }

        private static DialogueNode ReadNode(BinaryReader r, int index, int count)
        {
            var kind = r.ReadByte();
            var id = BinaryFormat.ReadGuid(r);
            var speaker = Ref(r, int.MaxValue) ;
            var text = new TextKey(r.ReadInt64());
            var note = r.ReadString();

            if (kind == (byte)DialogueNodeKind.Line)
            {
                var next = Ref(r, count);
                var label = new TextKey(r.ReadInt64());
                return DialogueNode.CreateLine(index, id, speaker, text, note, next, label);
            }
            if (kind != (byte)DialogueNodeKind.Choice) throw new DialogueFormatException($"A node has an unknown kind ({kind}).");

            var answerSpeaker = Ref(r, int.MaxValue);
            var optionCount = BinaryFormat.ReadVarInt(r);
            if (optionCount < 0 || optionCount > r.BaseStream.Length - r.BaseStream.Position) throw new DialogueFormatException("A choice says it has more answers than it can hold.");
            var options = new DialogueOption[optionCount];
            for (var o = 0; o < optionCount; o++)
            {
                var optionId = BinaryFormat.ReadGuid(r);
                var optionText = new TextKey(r.ReadInt64());
                var next = Ref(r, count);
                var label = new TextKey(r.ReadInt64());
                options[o] = new DialogueOption(optionId, optionText, next, label);
            }
            return DialogueNode.CreateChoice(index, id, speaker, text, note, answerSpeaker, options);
        }

        /// <summary>Reads a reference (an index plus one) and gives the index, -1 for 0. A node reference must be inside the graph.</summary>
        private static int Ref(BinaryReader r, int max)
        {
            var value = BinaryFormat.ReadVarInt(r);
            if (value < 0 || value > max) throw new DialogueFormatException("A link points at a node that is not there.");
            return value - 1;
        }
    }
}
