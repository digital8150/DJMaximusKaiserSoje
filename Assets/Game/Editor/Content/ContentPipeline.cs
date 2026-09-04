using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Moves the prototype's song content into the game's content folder and re-asserts the
    /// Addressables keys. Everything goes through the asset database so GUIDs survive and the
    /// catalog keeps resolving; nothing here touches serialized files by hand.
    /// </summary>
    internal static class ContentPipeline
    {
        private const string Label = "song-content";
        private const string ContentFolder = "Assets/Game/Content";
        private const string SongFolder = "Assets/Game/Content/Songs";
        private const string LegacyRoot = "Assets/RhythmPrototype";

        private static readonly IReadOnlyDictionary<string, string> Addresses = new Dictionary<string, string>
        {
            { "Assets/Game/Content/SongCatalog.json", "catalog.rhythm.bootstrap" },
            { "Assets/Game/Content/Music/Theme-Title.mp3", "theme.title" },
            { "Assets/Game/Content/Music/Theme-SongSelect.mp3", "theme.song-select" },
            { "Assets/Game/Content/Music/Theme-Result.mp3", "theme.result" },
            { SongFolder + "/ShootingStar-Easy.txt", "song.shooting-star.easy.beatmap" },
            { SongFolder + "/ShootingStar-Normal.txt", "song.shooting-star.normal.beatmap" },
            { SongFolder + "/ShootingStar-Hard.txt", "song.shooting-star.hard.beatmap" },
            { SongFolder + "/ShootingStar-Insane.txt", "song.shooting-star.insane.beatmap" },
            { SongFolder + "/ShootingStar-Satellite.txt", "song.shooting-star.satellite.beatmap" },
            { SongFolder + "/ShootingStar.ogg.bytes", "song.shooting-star.audio" },
            { SongFolder + "/ShootingStar-Cover-1.jpg", "song.shooting-star.cover.1" },
            { SongFolder + "/ShootingStar-Cover-2.jpg", "song.shooting-star.cover.2" },
            { SongFolder + "/ShootingStar-Cover-3.jpg", "song.shooting-star.cover.3" },
            { SongFolder + "/ShootingStar-BGA.mp4", "song.shooting-star.video" },
            { SongFolder + "/LastFortune-Lv1.txt", "song.last-fortune.lv1.beatmap" },
            { SongFolder + "/LastFortune-Lv20.txt", "song.last-fortune.lv20.beatmap" },
            { SongFolder + "/LastFortune-Lv45.txt", "song.last-fortune.lv45.beatmap" },
            { SongFolder + "/LastFortune.ogg.bytes", "song.last-fortune.audio" },
            { SongFolder + "/LastFortune-Cover.jpg", "song.last-fortune.cover" },
            { SongFolder + "/DisconnectedTrance-Insane.txt", "song.disconnected-trance.insane.beatmap" },
            { SongFolder + "/DisconnectedTrance-Stepmania.txt", "song.disconnected-trance.stepmania.beatmap" },
            { SongFolder + "/DisconnectedTrance.ogg.bytes", "song.disconnected-trance.audio" },
            { SongFolder + "/DisconnectedTrance-Cover.jpg", "song.disconnected-trance.cover" }
        };

        [MenuItem("Tools/DJ Maximus/3 · Migrate Content")]
        public static void Migrate()
        {
            EnsureFolder(ContentFolder);
            EnsureFolder(SongFolder);

            MoveLegacyContent();
            RetireLegacyPrototype();

            AssetDatabase.Refresh();
            ConfigureImporters();
            RegisterAddresses();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Song content migrated and Addressables keys re-asserted.");
        }

        private static void MoveLegacyContent()
        {
            const string legacyContent = LegacyRoot + "/Content";
            if (!AssetDatabase.IsValidFolder(legacyContent)) return;

            foreach (var path in Directory.GetFiles(legacyContent).Where(file => !file.EndsWith(".meta", StringComparison.Ordinal)))
            {
                string source = path.Replace('\\', '/');
                string name = Path.GetFileName(source);

                // The old plain-text catalog is superseded by the versioned JSON document.
                if (name.Equals("SongCatalog.txt", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.DeleteAsset(source);
                    continue;
                }

                string destination = SongFolder + "/" + name;
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination) != null)
                {
                    AssetDatabase.DeleteAsset(source);
                    continue;
                }

                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"Could not move {source}: {error}");
            }
        }

        /// <summary>
        /// Removes the prototype now that the game structure covers it. Its art is superseded by the
        /// generated chrome, so it is deleted rather than moved.
        /// </summary>
        private static void RetireLegacyPrototype()
        {
            foreach (var path in new[]
                     {
                         LegacyRoot + "/Runtime",
                         LegacyRoot + "/Editor",
                         LegacyRoot + "/Tests",
                         LegacyRoot + "/Art",
                         LegacyRoot + "/Content",
                         LegacyRoot + "/README.md",
                         "Assets/Scenes/RhythmPrototype.unity",
                         "Assets/Scenes/SampleScene.unity"
                     })
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null &&
                    !AssetDatabase.IsValidFolder(path))
                    continue;

                if (!AssetDatabase.DeleteAsset(path)) Debug.LogWarning("Could not delete " + path);
            }

            if (AssetDatabase.IsValidFolder(LegacyRoot)) AssetDatabase.DeleteAsset(LegacyRoot);
        }

        private static void ConfigureImporters()
        {
            foreach (var path in Addresses.Keys)
            {
                var importer = AssetImporter.GetAtPath(path);
                switch (importer)
                {
                    case TextureImporter texture:
                        texture.textureType = TextureImporterType.Sprite;
                        texture.spriteImportMode = SpriteImportMode.Single;
                        texture.mipmapEnabled = false;
                        texture.sRGBTexture = true;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.maxTextureSize = 1024;
                        texture.SaveAndReimport();
                        break;

                }
            }
        }

        private static void RegisterAddresses()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) throw new InvalidOperationException("Addressables settings could not be created.");

            settings.AddLabel(Label);
            var wanted = new HashSet<string>(Addresses.Values, StringComparer.Ordinal);

            foreach (var pair in Addresses)
            {
                string guid = AssetDatabase.AssetPathToGUID(pair.Key);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning("Missing content asset: " + pair.Key);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);
                entry.address = pair.Value;
                entry.SetLabel(Label, true, true, false);
            }

            // Drop entries the prototype left behind so two keys never claim the same address.
            foreach (var group in settings.groups.Where(group => group != null).ToArray())
            {
                foreach (var entry in group.entries.ToArray())
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    bool gone = string.IsNullOrEmpty(assetPath);
                    bool underPrototype = !gone && assetPath.StartsWith(LegacyRoot, StringComparison.Ordinal);
                    if (!gone && !underPrototype) continue;
                    settings.RemoveAssetEntry(entry.guid, false);
                }
            }

            if (wanted.Count == 0) Debug.LogWarning("No content addresses were registered.");

            EditorUtility.SetDirty(settings);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
