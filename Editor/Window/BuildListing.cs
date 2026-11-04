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
        /// <param name="syncedHash">The content hash of the build the catalog in the project came from (null or empty: nothing is synced).</param>
        public static BuildInfo Read(string filePath, string assetPath, string syncedHash)
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
                    info.BaseLanguage = m.baseLanguage;
                    info.Languages = m.languages;
                    info.Dialogues = m.dialogues.Count;
                    info.Speakers = m.speakers.Count;
                    info.IsSynced = !string.IsNullOrEmpty(syncedHash) && syncedHash == m.contentHash;
                }
            }
            catch (Exception e)
            {
                info.Error = e.Message;
            }
            return info;
        }

        /// <summary>
        /// Which build the project should be synced to without being asked, or null for none: nothing is done when the project already has one of the builds
        /// (several different builds in a project are the user's business: syncing them in turns would only undo each other), otherwise the newest readable one.
        /// </summary>
        public static BuildInfo PickToSync(IReadOnlyList<BuildInfo> builds)
        {
            if (builds.Any(b => b.Error == null && b.IsSynced)) return null;
            return builds.Where(b => b.Error == null).OrderByDescending(b => b.BuiltAt, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>Every .lorebuild under Assets, the newest build first (a damaged file last).</summary>
        public static IReadOnlyList<BuildInfo> FindAll(string syncedHash)
        {
            return LoreBuildSync.FindBuilds()
                .Select(assetPath => Read(Path.GetFullPath(assetPath), assetPath, syncedHash))
                .OrderBy(b => b.Error != null)
                .ThenByDescending(b => b.BuiltAt, StringComparer.Ordinal)
                .ToList();
        }
    }
}
