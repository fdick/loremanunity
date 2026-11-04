using System;
using System.Collections.Generic;

namespace Loreman.Dialogues.Editor
{
    // The manifest.json of a .lorebuild, read with JsonUtility (so the names are the ones in the file: camelCase).

    [Serializable]
    public sealed class ManifestLanguage
    {
        public string code;
        public int total;
        public int translated;
        public int stale;
    }

    [Serializable]
    public sealed class ManifestAudio
    {
        /// <summary>The id of the line or of the answer the voice-over belongs to.</summary>
        public string id;

        /// <summary>The key id of the voice-over in the audio table.</summary>
        public long key;

        public string language;
        public string path;

        public Guid Guid => Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
    }

    [Serializable]
    public sealed class ManifestDialogue
    {
        public string id;
        public string name;
        public string groupId;
        public string graph;
        public string hash;
        public int nodes;
        public int texts;
        public List<ManifestAudio> audio = new List<ManifestAudio>();

        public Guid Guid => Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
    }

    [Serializable]
    public sealed class BuildManifest
    {
        public string format;
        public int formatVersion;
        public string projectId;
        public string projectName;
        public string builtAt;
        public string contentHash;
        public string baseLanguage;
        public List<ManifestLanguage> languages = new List<ManifestLanguage>();
        public List<CatalogSpeaker> speakers = new List<CatalogSpeaker>();
        public List<CatalogGroup> groups = new List<CatalogGroup>();
        public List<ManifestDialogue> dialogues = new List<ManifestDialogue>();
    }
}
