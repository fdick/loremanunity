using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Loreman.Dialogues;

namespace Loreman.Items
{
    /// <summary>
    /// Reads <c>items/items.bin</c> of a build into <see cref="ItemsData"/>. The layout is in Documentation~/BUILD_FORMAT.md; a reference is an index plus one, so 0 means “nothing”.
    /// </summary>
    public static class ItemsReader
    {
        public const byte SupportedVersion = 1;

        private static readonly byte[] Magic = { (byte)'L', (byte)'I', (byte)'T', (byte)'M' };

        // The smallest item takes 36 bytes; the limits keep a damaged count from asking for a huge list.
        private const int MinItemBytes = 30;
        private const int MinOtherBytes = 1;

        public static ItemsData Read(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var items = ReadAll(reader, data.Length);
                    if (stream.Position != stream.Length) throw new DialogueFormatException("The item data has bytes left over.");
                    return items;
                }
            }
            catch (EndOfStreamException e)
            {
                throw new DialogueFormatException("The item data ends too early.", e);
            }
        }

        private static int Count(BinaryReader r, int length, int minBytes, string what)
        {
            var count = BinaryFormat.ReadVarInt(r);
            if (count < 0 || count > length / minBytes) throw new DialogueFormatException("The item data says it has more " + what + " than it can hold.");
            return count;
        }

        private static ItemsData ReadAll(BinaryReader r, int length)
        {
            if (!BinaryFormat.ReadMagic(r, Magic)) throw new DialogueFormatException("This is not the catalog of the items of a build.");
            var version = r.ReadByte();
            if (version < 1 || version > SupportedVersion)
                throw new DialogueFormatException($"The item data is of version {version}; this package reads version {SupportedVersion}. Update the package or build the project again.");

            var stats = new ItemStatDefinition[Count(r, length, 2, "number parameters")];
            for (var i = 0; i < stats.Length; i++) stats[i] = new ItemStatDefinition(r.ReadString(), r.ReadString());

            var fieldNames = new string[Count(r, length, MinOtherBytes, "text parameters")];
            for (var i = 0; i < fieldNames.Length; i++) fieldNames[i] = r.ReadString();

            var folders = new ItemFolderData[Count(r, length, 18, "folders")];
            for (var i = 0; i < folders.Length; i++)
            {
                var id = BinaryFormat.ReadGuid(r);
                var parent = BinaryFormat.ReadVarInt(r);
                // a parent comes before its children, so a reference points back
                if (parent < 0 || parent > i) throw new DialogueFormatException("A folder of the item data has a parent that is not before it.");
                folders[i] = new ItemFolderData(id, parent - 1, r.ReadString());
            }

            var icons = new string[Count(r, length, MinOtherBytes, "pictures")];
            for (var i = 0; i < icons.Length; i++) icons[i] = r.ReadString();

            var items = new ItemData[Count(r, length, MinItemBytes, "items")];
            for (var i = 0; i < items.Length; i++) items[i] = ReadItem(r, stats.Length, fieldNames, folders.Length, icons.Length);
            return new ItemsData(stats, folders, icons, items);
        }

        private static ItemData ReadItem(BinaryReader r, int statCount, string[] fieldNames, int folderCount, int iconCount)
        {
            var id = BinaryFormat.ReadGuid(r);
            var folder = BinaryFormat.ReadVarInt(r);
            if (folder < 0 || folder > folderCount) throw new DialogueFormatException("An item is in a folder that is not there.");
            var name = new TextKey(r.ReadInt64());
            var description = new TextKey(r.ReadInt64());
            var icon = BinaryFormat.ReadVarInt(r);
            if (icon < 0 || icon > iconCount) throw new DialogueFormatException("An item has a picture that is not there.");

            var stats = new ItemStatValue[BinaryFormat.ReadVarInt(r)];
            for (var i = 0; i < stats.Length; i++)
            {
                var index = BinaryFormat.ReadVarInt(r);
                if (index < 0 || index >= statCount) throw new DialogueFormatException("An item has a number parameter that is not there.");
                stats[i] = new ItemStatValue(index, ReadNumber(r));
            }

            var fields = new ItemFieldValue[BinaryFormat.ReadVarInt(r)];
            for (var i = 0; i < fields.Length; i++)
            {
                var index = BinaryFormat.ReadVarInt(r);
                if (index < 0 || index >= fieldNames.Length) throw new DialogueFormatException("An item has a text parameter that is not there.");
                fields[i] = new ItemFieldValue(fieldNames[index], new TextKey(r.ReadInt64()));
            }
            return new ItemData(id, folder - 1, name, description, icon - 1, stats, fields);
        }

        /// <summary>A whole number is a zigzag varint (small numbers of either sign take a byte or two), anything else a double.</summary>
        private static double ReadNumber(BinaryReader r)
        {
            var kind = r.ReadByte();
            if (kind == 1) return r.ReadDouble();
            if (kind != 0) throw new DialogueFormatException("A number of the item data is of a kind this package does not know.");
            var zigzag = BinaryFormat.ReadVarULong(r);
            return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
        }
    }
}
