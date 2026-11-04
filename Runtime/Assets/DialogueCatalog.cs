using System;
using System.Collections.Generic;
using UnityEngine;

namespace Loreman.Dialogues
{
    [Serializable]
    public sealed class CatalogSpeaker
    {
        public string id;
        public string color;
        public long nameKey;
        public long noteKey;
    }

    [Serializable]
    public sealed class CatalogGroup
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class CatalogDialogue
    {
        public string id;
        public string name;
        public string groupId;
        public string hash;
        public int nodes;

        public Guid Guid => Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>A speaker as the game sees it.</summary>
    public readonly struct SpeakerInfo
    {
        public SpeakerInfo(Guid id, Color color, TextKey name, TextKey note)
        {
            Id = id;
            Color = color;
            Name = name;
            Note = note;
        }

        public Guid Id { get; }

        /// <summary>The colour of the speaker's lines in Loreman: handy for a name plate.</summary>
        public Color Color { get; }

        /// <summary>Where the name is in the speakers table.</summary>
        public TextKey Name { get; }

        /// <summary>Where the description is, or <see cref="TextKey.None"/>.</summary>
        public TextKey Note { get; }
    }

    /// <summary>
    /// The list of everything a build holds, small enough to be loaded at the start of the game: the dialogues (name, group, code), the speakers and the languages.
    /// The graphs and the texts are not here: each dialogue loads its own when it is opened. The importer makes this asset.
    /// </summary>
    public sealed class DialogueCatalog : ScriptableObject
    {
        [SerializeField] private string projectId;
        [SerializeField] private string projectName;
        [SerializeField] private string contentHash;
        [SerializeField] private string baseLanguage;
        [SerializeField] private List<string> languages = new List<string>();
        [SerializeField] private List<CatalogSpeaker> speakers = new List<CatalogSpeaker>();
        [SerializeField] private List<CatalogGroup> groups = new List<CatalogGroup>();
        [SerializeField] private List<CatalogDialogue> dialogues = new List<CatalogDialogue>();

        [NonSerialized] private Dictionary<Guid, CatalogDialogue> _index;

        /// <summary>The id of the Loreman project this catalog was made from. Builds of different projects have catalogs of their own.</summary>
        public Guid ProjectGuid => Guid.TryParse(projectId, out var guid) ? guid : Guid.Empty;

        public string ProjectName => projectName;

        /// <summary>The code of the build this catalog was made from.</summary>
        public string ContentHash => contentHash;

        /// <summary>The language the project is written in (the first language of the project).</summary>
        public string BaseLanguage => baseLanguage;

        /// <summary>The base language and the ones the project is translated into, as Loreman names them (<c>en</c>, <c>pt-br</c>).</summary>
        public IReadOnlyList<string> Languages => languages;

        public IReadOnlyList<CatalogDialogue> Dialogues => dialogues;

        public IReadOnlyList<CatalogGroup> Groups => groups;

        public int SpeakerCount => speakers.Count;

        public bool TryGetDialogue(Guid id, out CatalogDialogue dialogue)
        {
            if (_index == null)
            {
                _index = new Dictionary<Guid, CatalogDialogue>(dialogues.Count);
                foreach (var d in dialogues) _index[d.Guid] = d;
            }
            return _index.TryGetValue(id, out dialogue);
        }

        /// <summary>A speaker by the index a node holds. Returns false for -1 (nobody) and for an index the catalog does not have.</summary>
        public bool TryGetSpeaker(int index, out SpeakerInfo speaker)
        {
            if (index < 0 || index >= speakers.Count)
            {
                speaker = default;
                return false;
            }
            var s = speakers[index];
            if (!ColorUtility.TryParseHtmlString(s.color, out var color)) color = Color.white;
            speaker = new SpeakerInfo(Guid.TryParse(s.id, out var id) ? id : Guid.Empty, color, new TextKey(s.nameKey), new TextKey(s.noteKey));
            return true;
        }

        /// <summary>Fills the catalog. Called by the importer.</summary>
        public void Populate(
            string projectGuid, string project, string hash, string baseCode, IEnumerable<string> languageCodes,
            IEnumerable<CatalogSpeaker> speakerList, IEnumerable<CatalogGroup> groupList, IEnumerable<CatalogDialogue> dialogueList)
        {
            projectId = projectGuid;
            projectName = project;
            contentHash = hash;
            baseLanguage = baseCode;
            languages = new List<string>(languageCodes);
            speakers = new List<CatalogSpeaker>(speakerList);
            groups = new List<CatalogGroup>(groupList);
            dialogues = new List<CatalogDialogue>(dialogueList);
            _index = null;
        }

        private void OnEnable() => _index = null;
    }
}
