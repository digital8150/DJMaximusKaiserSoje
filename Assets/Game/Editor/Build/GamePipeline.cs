using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Builds every authored asset from script. The screens are described in code and stamped into
    /// scenes by Unity itself, so nobody has to hand-edit serialized YAML and a fresh clone can
    /// rebuild the whole front end with one command.
    /// </summary>
    public static class GamePipeline
    {
        /// <summary>Fonts and sprites the screens reference. Run this before building screens.</summary>
        [MenuItem("Tools/DJ Maximus/1 · Prepare Assets")]
        public static void PrepareAssets()
        {
            TmpEssentials.Import();
            ConfigureMusicImporters();
            ChromeSpriteGenerator.Generate();
            ConfigureGeneratedArtImporters();
            FontAssetBuilder.Build();
            Ui.ResetCaches();
            Debug.Log("Assets prepared.");
        }

        /// <summary>Prefabs, screen scenes, and the build settings scene list.</summary>
        [MenuItem("Tools/DJ Maximus/2 · Build Screens")]
        public static void BuildScreens()
        {
            Ui.ResetCaches();

            var scenes = new List<string>
            {
                BootSceneBuilder.Build(),
                TitleSceneBuilder.Build(),
                OptionsSceneBuilder.Build(),
                SongSelectSceneBuilder.Build(),
                GameplaySceneBuilder.Build(),
                ResultSceneBuilder.Build()
            };

            EditorBuildSettings.scenes = scenes
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Screens built: " + string.Join(", ", scenes));
        }

        [MenuItem("Tools/DJ Maximus/Build Everything")]
        public static void BuildAll()
        {
            PrepareAssets();
            BuildScreens();
        }

        /// <summary>Rebuilds only the scenes touched by settings navigation.</summary>
        public static void BuildSettingsEntryScenes()
        {
            Ui.ResetCaches();
            TitleSceneBuilder.Build();
            SongSelectSceneBuilder.Build();
            OptionsSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Settings entry scenes built.");
        }

        /// <summary>Rebuilds the two screens that share play-setting presentation.</summary>
        public static void BuildPlaySettingsScenes()
        {
            Ui.ResetCaches();
            OptionsSceneBuilder.Build();
            GameplaySceneBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Play settings scenes built.");
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
