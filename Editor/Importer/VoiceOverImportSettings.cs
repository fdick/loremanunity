using System.IO;
using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// Imports the voice-overs of a build as game audio. Unity's defaults are made for music and effects (stereo, high quality, the whole clip decompressed in memory);
    /// a recording of a voice needs one channel, a modest Vorbis quality, and, when it is long, streaming. This is applied once, when a recording is first imported
    /// (see <see cref="LoremanSettings.optimizeVoiceOvers"/>), and marked, so that a change you make to a clip's own import settings afterwards is kept.
    /// </summary>
    public sealed class VoiceOverImportSettings : AssetPostprocessor
    {
        internal const string Marker = "loreman-voice";

        /// <summary>The settings of the sync that is writing recordings right now (a postprocessor cannot be handed anything); otherwise the project's own settings asset is used.</summary>
        internal static LoremanSettings Active;

        private void OnPreprocessAudio()
        {
            var settings = Active != null ? Active : LoremanSettings.Find();
            if (settings == null || !settings.optimizeVoiceOvers) return;
            if (!assetPath.StartsWith(settings.outputFolder.TrimEnd('/') + "/Audio/")) return;

            var importer = (AudioImporter)assetImporter;
            if (importer.userData == Marker) return; // already done, perhaps changed by hand since
            Apply(importer, settings, new FileInfo(assetPath).Length);
            importer.userData = Marker;
        }

        /// <summary>A big recording is streamed from disk when played; a small one is kept compressed in memory, which starts at once.</summary>
        public static AudioClipLoadType LoadTypeFor(LoremanSettings settings, long fileBytes) =>
            fileBytes > settings.streamLargerThanKB * 1024L ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;

        /// <summary>Sets the importer for speech: Vorbis, mono, streamed when the file is big.</summary>
        public static void Apply(AudioImporter importer, LoremanSettings settings, long fileBytes)
        {
            var sample = importer.defaultSampleSettings;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = Mathf.Clamp(settings.voiceQuality, 0.05f, 1f);
            sample.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            sample.loadType = LoadTypeFor(settings, fileBytes);
            sample.preloadAudioData = false; // it is loaded when the line is about to be said, not with the scene
            importer.defaultSampleSettings = sample;
            importer.forceToMono = settings.forceMono;
            importer.loadInBackground = true;
        }
    }
}
