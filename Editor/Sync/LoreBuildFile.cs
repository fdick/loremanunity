using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// An opened .lorebuild file (a ZIP; see Documentation~/BUILD_FORMAT.md): the manifest and the parts, read by their paths. Only the editor reads builds:
    /// the game gets what the importer made of them.
    /// </summary>
    public sealed class LoreBuildFile : IDisposable
    {
        public const string FormatName = "loreman.build";

        /// <summary>The newest version of the file this package reads.</summary>
        public const int SupportedVersion = 1;

        private readonly ZipArchive _zip;

        private LoreBuildFile(ZipArchive zip, BuildManifest manifest)
        {
            _zip = zip;
            Manifest = manifest;
        }

        public BuildManifest Manifest { get; }

        public static string GraphPath(Guid dialogueId) => "dialogues/" + dialogueId.ToString("N") + ".bin";

        public static string TextsPath(string language, Guid dialogueId) => "texts/" + language + "/" + dialogueId.ToString("N") + ".bin";

        public static string SpeakerTextsPath(string language) => "texts/" + language + "/speakers.bin";

        public static LoreBuildFile Open(string path)
        {
            // FileShare.ReadWrite: Loreman or the browser may still be finishing the file
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try
            {
                return Open(stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>Opens a build from a stream (the stream is closed with the file).</summary>
        public static LoreBuildFile Open(Stream stream)
        {
            ZipArchive zip;
            try
            {
                zip = new ZipArchive(stream, ZipArchiveMode.Read, false);
            }
            catch (InvalidDataException e)
            {
                throw new DialogueFormatException("This is not a .lorebuild file made by Loreman: it is not a ZIP archive.", e);
            }

            try
            {
                var entry = zip.GetEntry("manifest.json") ?? throw new DialogueFormatException("This is not a .lorebuild file: manifest.json is missing.");
                string json;
                using (var reader = new StreamReader(entry.Open()))
                    json = reader.ReadToEnd();

                var manifest = JsonUtility.FromJson<BuildManifest>(json);
                if (manifest == null || manifest.format != FormatName) throw new DialogueFormatException("This is not a .lorebuild file made by Loreman.");
                if (manifest.formatVersion > SupportedVersion)
                    throw new DialogueFormatException(
                        $"This build is of version {manifest.formatVersion}; this package reads version {SupportedVersion}. Update the Loreman Dialogues package.");
                return new LoreBuildFile(zip, manifest);
            }
            catch
            {
                zip.Dispose();
                throw;
            }
        }

        public bool Has(string path) => _zip.GetEntry(path) != null;

        public byte[] Read(string path)
        {
            var entry = _zip.GetEntry(path) ?? throw new DialogueFormatException($"The build has no part \"{path}\".");
            using (var stream = entry.Open())
            using (var ms = new MemoryStream((int)Math.Max(0, entry.Length)))
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public void Dispose() => _zip.Dispose();
    }
}
