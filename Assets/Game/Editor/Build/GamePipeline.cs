using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Prepares authored runtime assets (fonts, music importers, chrome sprites) from script.
    /// </summary>
    public static class GamePipeline
    {
        /// <summary>Fonts and sprites the screens reference.</summary>
        [MenuItem("Tools/DJ Maximus/Prepare Assets")]
        public static void PrepareAssets()
        {
            TmpEssentials.Import();
            ConfigureMusicImporters();
            ChromeSpriteGenerator.Generate();
            ConfigureGeneratedArtImporters();
            FontAssetBuilder.Build();
            Debug.Log("Assets prepared.");
        }

        /// <summary>
        /// Screen music is long and only one track plays at a time, so it streams rather than
        /// sitting decompressed in memory next to the chart audio.
        /// </summary>
        private static void ConfigureMusicImporters()
        {
            foreach (var path in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game/Content/Music" })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;

                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                settings.preloadAudioData = false;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureGeneratedArtImporters()
        {
            const string folder = "Assets/Game/UI/Art/Generated";
            if (!AssetDatabase.IsValidFolder(folder)) return;

            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }
    }
}
