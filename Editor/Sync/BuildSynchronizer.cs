using System;
using System.Collections.Generic;

namespace Loreman.Dialogues.Editor
{
    /// <summary>A voice-over of a dialogue: whose it is, the language, and a way to read the recording (read only when it is written, so a big build is not held in memory).</summary>
    public sealed class SyncAudioFile
    {
        public SyncAudioFile(Guid id, long key, string language, string extension, Func<byte[]> read)
        {
            Id = id;
            Key = key;
            Language = language;
            Extension = extension;
            Read = read;
        }

        /// <summary>The id of the line or the answer.</summary>
        public Guid Id { get; }

        /// <summary>The key id in the audio table.</summary>
        public long Key { get; }

        /// <summary>The language in the build's spelling (<c>pt-br</c>).</summary>
        public string Language { get; }

        /// <summary>The file extension without the dot: mp3, wav, ogg, m4a.</summary>
        public string Extension { get; }

        public Func<byte[]> Read { get; }
    }

    /// <summary>Where a sync puts what it reads from a build. <see cref="UnitySyncTarget"/> writes assets, Addressables entries and Localization tables; a test can put a fake.</summary>
    public interface ISyncTarget
    {
        /// <summary>The code of the build the catalog in the project came from (empty when there is no catalog).</summary>
        string ContentHash { get; }

        /// <summary>The dialogues that are in the project now (their assets exist), with the code of the build each came from.</summary>
        IReadOnlyDictionary<Guid, string> ExistingDialogues();

        /// <summary>A sync begins and ends: the target may group its file writes between the two.</summary>
        void Begin();

        void End();

        /// <summary>Makes sure the project has the locales of these languages (Loreman codes).</summary>
        void EnsureLocales(IReadOnlyList<string> languages);

        /// <param name="texts">The texts of the dialogue by language (a language the dialogue has no text in is missing).</param>
        /// <param name="audio">The voice-overs of the dialogue (empty when it has none). Whatever the project had of this dialogue's audio before is replaced.</param>
        void WriteDialogue(ManifestDialogue dialogue, byte[] graph, IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts, IReadOnlyList<SyncAudioFile> audio);

        void RemoveDialogue(Guid dialogueId);

        void WriteSpeakers(IReadOnlyDictionary<string, IReadOnlyList<StringTableEntry>> texts);

        void WriteCatalog(BuildManifest manifest);
    }

    /// <summary>What a sync did.</summary>
    public sealed class SyncReport
    {
        public int Added { get; internal set; }

        public int Updated { get; internal set; }

        public int Removed { get; internal set; }

        public int Unchanged { get; internal set; }

        /// <summary>The build is the one the project already has: nothing was written.</summary>
        public bool WasUpToDate { get; internal set; }

        public override string ToString() =>
            WasUpToDate
                ? "Loreman: dialogues are up to date."
                : $"Loreman: dialogues synced — {Added} added, {Updated} updated, {Removed} removed, {Unchanged} unchanged.";
    }

    /// <summary>
    /// Reads a build and brings the project to it: only what changed is written (see <see cref="SyncPlan"/>). The rules live here; where things are written
    /// is the business of <see cref="ISyncTarget"/>.
    /// </summary>
    public static class BuildSynchronizer
    {
        public static SyncReport Run(LoreBuildFile file, ISyncTarget target, bool force = false)
        {
            var manifest = file.Manifest;
            var languages = new List<string>();
            foreach (var l in manifest.languages) languages.Add(l.code);
            if (!languages.Contains(manifest.baseLanguage)) languages.Insert(0, manifest.baseLanguage);

            var plan = SyncPlan.Compute(manifest.dialogues, target.ExistingDialogues());
            var report = new SyncReport
            {
                Added = plan.ToAdd.Count,
                Updated = plan.ToUpdate.Count,
                Removed = plan.ToRemove.Count,
                Unchanged = plan.Unchanged,
            };
            if (!force && plan.IsEmpty && target.ContentHash == manifest.contentHash)
            {
                report.WasUpToDate = true;
                return report;
            }

            // Everything is read and checked before the first file is written: a damaged build changes nothing.
            var work = new List<(ManifestDialogue Dialogue, byte[] Graph, Dictionary<string, IReadOnlyList<StringTableEntry>> Texts, List<SyncAudioFile> Audio)>();
            foreach (var d in force ? PlanAll(manifest) : plan.ToWrite)
            {
                var graph = file.Read(string.IsNullOrEmpty(d.graph) ? LoreBuildFile.GraphPath(d.Guid) : d.graph);
                DialogueGraphReader.Read(graph);
                var audio = new List<SyncAudioFile>();
                foreach (var a in d.audio)
                {
                    if (!file.Has(a.path)) throw new DialogueFormatException("The build has no part \"" + a.path + "\".");
                    var path = a.path;
                    audio.Add(new SyncAudioFile(a.Guid, a.key, a.language, System.IO.Path.GetExtension(path).TrimStart('.'), () => file.Read(path)));
                }
                work.Add((d, graph, ReadTexts(file, languages, l => LoreBuildFile.TextsPath(l, d.Guid)), audio));
            }
            var speakers = ReadTexts(file, languages, LoreBuildFile.SpeakerTextsPath);

            target.Begin();
            try
            {
                target.EnsureLocales(languages);
                foreach (var w in work) target.WriteDialogue(w.Dialogue, w.Graph, w.Texts, w.Audio);
                foreach (var id in plan.ToRemove) target.RemoveDialogue(id);
                target.WriteSpeakers(speakers);
                target.WriteCatalog(manifest);
            }
            finally
            {
                target.End();
            }
            return report;
        }

        private static IEnumerable<ManifestDialogue> PlanAll(BuildManifest manifest) => manifest.dialogues;

        private static Dictionary<string, IReadOnlyList<StringTableEntry>> ReadTexts(LoreBuildFile file, IReadOnlyList<string> languages, Func<string, string> path)
        {
            var result = new Dictionary<string, IReadOnlyList<StringTableEntry>>();
            foreach (var language in languages)
            {
                var p = path(language);
                if (file.Has(p)) result[language] = StringTableReader.Read(file.Read(p));
            }
            return result;
        }
    }
}
