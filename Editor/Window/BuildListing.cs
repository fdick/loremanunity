using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Loreman.Dialogues.Editor
{
    /// <summary>What the window knows of a .lorebuild file in the project.</summary>
    public sealed class BuildInfo
    {
        /// <summary>"Assets/…/game.lorebuild".</summary>
        public string AssetPath { get; set; }

        public string FileName => Path.GetFileName(AssetPath);

        public string ProjectName { get; set; }

        public string BuiltAt { get; set; }

        public string ContentHash { get; set; }

        /// <summary>The Loreman project the build is of. Builds of one project replace each other; builds of different projects live side by side.</summary>
        public Guid ProjectId { get; set; }

        public string BaseLanguage { get; set; }

        public IReadOnlyList<ManifestLanguage> Languages { get; set; } = Array.Empty<ManifestLanguage>();

        public int Dialogues { get; set; }

        public int Speakers { get; set; }

        /// <summary>Why the file cannot be read (damaged, not a build, a newer version), or null.</summary>
        public string Error { get; set; }

        /// <summary>The project already has exactly this build (its catalog was made from it).</summary>
        public bool IsSynced { get; set; }
    }

    /// <summary>Finds the builds of the project and reads their manifests (only the manifest: nothing else is opened).</summary>
    public static class BuildListing
    {
        /// <param name="syncedHashes">The content hash of the build each project's catalog came from (a project missing here is not synced).</param>
        public static BuildInfo Read(string filePath, string assetPath, IReadOnlyDictionary<Guid, string> syncedHashes)
        {
            var info = new BuildInfo { AssetPath = assetPath };
            try
            {
                using (var file = LoreBuildFile.Open(filePath))
                {
                    var m = file.Manifest;
                    info.ProjectName = m.projectName;
                    info.BuiltAt = m.builtAt;
                    info.ContentHash = m.contentHash;
                    Guid.TryParse(m.projectId, out var projectId);
                    info.ProjectId = projectId;
                    info.BaseLanguage = m.baseLanguage;
                    info.Languages = m.languages;
                    info.Dialogues = m.dialogues.Count;
                    info.Speakers = m.speakers.Count;
                    info.IsSynced = syncedHashes != null && syncedHashes.TryGetValue(projectId, out var hash) && !string.IsNullOrEmpty(hash) && hash == m.contentHash;
                }
            }
            catch (Exception e)
            {
                info.Error = e.Message;
            }
            return info;
        }

        /// <summary>
        /// The builds to sync without being asked: for each Loreman project, its newest readable build, unless the project in Unity is already at one of its builds
        /// (two files of one project would only undo each other). Builds of different projects are all synced: they live side by side.
        /// </summary>
        public static IReadOnlyList<BuildInfo> PickToSync(IReadOnlyList<BuildInfo> builds)
        {
            return builds
                .Where(b => b.Error == null && b.ProjectId != Guid.Empty)
                .GroupBy(b => b.ProjectId)
                .Where(g => !g.Any(b => b.IsSynced))
                .Select(g => g.OrderByDescending(b => b.BuiltAt, StringComparer.Ordinal).First())
                .OrderBy(b => b.AssetPath, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Every .lorebuild under Assets, the newest build first (a damaged file last).</summary>
        public static IReadOnlyList<BuildInfo> FindAll(IReadOnlyDictionary<Guid, string> syncedHashes)
        {
            return LoreBuildSync.FindBuilds()
                .Select(assetPath => Read(Path.GetFullPath(assetPath), assetPath, syncedHashes))
                .OrderBy(b => b.Error != null)
                .ThenByDescending(b => b.BuiltAt, StringComparer.Ordinal)
                .ToList();
        }
    }
}
