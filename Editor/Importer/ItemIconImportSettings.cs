using UnityEditor;
using UnityEngine;

namespace Loreman.Dialogues.Editor
{
    /// <summary>
    /// Imports the pictures of the items as sprites: the game loads them as <see cref="Sprite"/>s for its inventory screen. A picture is small (Loreman shrinks it to 192 pixels),
    /// so there are no mip maps. This is applied once, when a picture is first imported, and marked, so that a change made to a picture's own import settings afterwards is kept.
    /// </summary>
    public sealed class ItemIconImportSettings : AssetPostprocessor
    {
        internal const string Marker = "loreman-item";

        private void OnPreprocessTexture()
        {
            var settings = VoiceOverImportSettings.Active != null ? VoiceOverImportSettings.Active : LoremanSettings.Find();
            if (settings == null) return;
            var root = settings.outputFolder.TrimEnd('/') + "/Items/";
            if (!assetPath.StartsWith(root) || !assetPath.Contains("/Icons/")) return;

            var importer = (TextureImporter)assetImporter;
            if (importer.userData == Marker) return;
            Apply(importer, settings);
            importer.userData = Marker;
        }

        public static void Apply(TextureImporter importer, LoremanSettings settings)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = settings.compressItemIcons ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.maxTextureSize = 512;
        }
    }
}
