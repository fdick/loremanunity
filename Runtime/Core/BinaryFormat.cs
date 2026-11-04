using System;
using System.IO;

namespace Loreman.Dialogues
{
    /// <summary>The few things the binary parts of a build are made of (see Documentation~/BUILD_FORMAT.md).</summary>
    internal static class BinaryFormat
    {
        /// <summary>A number in the 7-bit format: seven bits per byte, the high bit says that another byte follows.</summary>
        public static int ReadVarInt(BinaryReader reader)
        {
            var result = 0;
            for (var shift = 0; shift < 35; shift += 7)
            {
                var b = reader.ReadByte();
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
            }
            throw new DialogueFormatException("A number in the data is too long.");
        }

        public static Guid ReadGuid(BinaryReader reader) => new Guid(reader.ReadBytes(16));

        public static bool ReadMagic(BinaryReader reader, byte[] magic)
        {
            var bytes = reader.ReadBytes(magic.Length);
            if (bytes.Length != magic.Length) return false;
            for (var i = 0; i < magic.Length; i++)
                if (bytes[i] != magic[i]) return false;
            return true;
        }
    }
}
