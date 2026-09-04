using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Imports one osu!mania set into project assets, updates the versioned remote catalog, assigns
    /// stable Addressables keys, and builds player content. Imported files remain ordinary Unity
    /// assets so the generated catalog can be rebuilt or uploaded by the release pipeline.
    /// </summary>
    public static class OszAddressableImporter
    {
        public const string RemoteCatalogAddress = "catalog.rhythm.remote";

        private const string BootstrapCatalogPath = "Assets/Game/Content/SongCatalog.json";
        private const string RemoteCatalogPath = "Assets/Game/Content/RemoteSongCatalog.json";
        private const string ImportRoot = "Assets/Game/Content/Songs/Imported";
        private const string RemoteGroupName = "Remote Song Content";
        private const string ContentLabel = "song-content";
        private const string RemoteLabel = "remote-song-content";
        private const string DefaultRemoteLoadPath = "http://localhost:8000/[BuildTarget]";

        private sealed class PreparedChart
        {
            public OszChartSource Source;
            public DifficultyTier Tier;
            public int Level;
            public string AssetPath;
            public string Address;
        }

        [MenuItem("Tools/DJ Maximus/Import osu!mania .osz")]
        public static void ImportWithPicker()
        {
            string path = EditorUtility.OpenFilePanel("osu!mania 곡 가져오기", string.Empty, "osz");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string songId = ImportAndBuild(path);
                EditorUtility.DisplayDialog("가져오기 완료",
                    "곡 데이터를 만들었습니다.\n\nID: " + songId + "\n출력: ServerData", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("가져오지 못했어요", exception.Message, "확인");
            }
        }

        public static string ImportAndBuild(string oszPath)
        {
            if (!string.Equals(Path.GetExtension(oszPath), ".osz", StringComparison.OrdinalIgnoreCase))
                throw new OszImportException(".osz 파일을 선택해 주세요.");

            OszBeatmapPackage package = OszBeatmapPackageReader.Read(oszPath);
            string songId = BuildSongId(oszPath, package);
            string songFolder = ImportRoot + "/" + songId;
            EnsureFolder(ImportRoot);
            if (AssetDatabase.IsValidFolder(songFolder) && !AssetDatabase.DeleteAsset(songFolder))
                throw new OszImportException("기존 가져오기 폴더를 갱신할 수 없습니다: " + songFolder);
            EnsureFolder(songFolder);

            var addresses = new Dictionary<string, string>(StringComparer.Ordinal);
            string addressPrefix = "song." + songId;
            string audioPath = WriteBinary(songFolder, "audio", package.Audio);
            string jacketPath = WriteBinary(songFolder, "jacket", package.Jacket);
            string videoPath = package.Video == null ? null : WriteBinary(songFolder, "video", package.Video);
            addresses.Add(audioPath, addressPrefix + ".audio");
            addresses.Add(jacketPath, addressPrefix + ".jacket");
            if (videoPath != null) addresses.Add(videoPath, addressPrefix + ".video");

            List<PreparedChart> charts = PrepareCharts(package, songFolder, addressPrefix, addresses);
            if (charts.Count == 0) throw new OszImportException("가져올 수 있는 난이도가 없습니다.");

            SongCatalogDocument catalog = LoadCatalog();
            SongCatalogEntry importedSong = CreateCatalogEntry(songId, package, charts,
                addresses[audioPath], addresses[jacketPath], videoPath == null ? string.Empty : addresses[videoPath]);
            catalog.songs = catalog.Songs.Where(song => song != null && song.id != songId)
                .Concat(new[] { importedSong }).ToArray();
            catalog.schemaVersion = SongCatalogParser.SupportedSchemaVersion;
            WriteTextAsset(RemoteCatalogPath, JsonUtility.ToJson(catalog, true) + Environment.NewLine);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporters(audioPath, jacketPath, videoPath);
            addresses.Add(RemoteCatalogPath, RemoteCatalogAddress);
            RegisterRemoteAddresses(addresses);
            AssetDatabase.SaveAssets();

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrWhiteSpace(result.Error))
                throw new OszImportException("Addressables 빌드에 실패했습니다: " + result.Error);

            Debug.Log("Imported " + package.Title + " (" + songId + ") with " + charts.Count +
                      " charts and built Addressables content.");
            return songId;
        }

        private static List<PreparedChart> PrepareCharts(OszBeatmapPackage package, string songFolder,
            string addressPrefix, IDictionary<string, string> addresses)
        {
            var result = new List<PreparedChart>();
            foreach (IGrouping<int, OszChartSource> group in package.Charts.GroupBy(chart => chart.Beatmap.Header.KeyCount))
            {
                var usedTiers = new HashSet<DifficultyTier>();
                int nameIndex = 0;
                foreach (OszChartSource source in group.OrderBy(chart => chart.Beatmap.Notes.Count))
                {
                    DifficultyTier inferred = SongCatalogParser.InferTier(source.Beatmap.Header.DifficultyName);
                    if (!TryReserveTier(inferred, usedTiers, out DifficultyTier tier))
                    {
                        Debug.LogWarning("Skipped extra " + group.Key + "K chart: " +
                                         source.Beatmap.Header.DifficultyName);
                        continue;
                    }

                    int level = SongCatalogParser.InferLevel(source.Beatmap.Header.DifficultyName, tier);
                    string chartSlug = Slug(source.Beatmap.Header.DifficultyName, "chart") + "-" + (++nameIndex);
                    string assetPath = songFolder + "/" + group.Key + "k-" + chartSlug + ".txt";
                    string address = addressPrefix + "." + group.Key + "k." + chartSlug + ".beatmap";
                    WriteTextAsset(assetPath, source.Text);
                    addresses.Add(assetPath, address);
                    result.Add(new PreparedChart
                    {
                        Source = source,
                        Tier = tier,
                        Level = level,
                        AssetPath = assetPath,
                        Address = address
                    });
                }
            }
            return result;
        }

        private static bool TryReserveTier(DifficultyTier preferred, ISet<DifficultyTier> used,
            out DifficultyTier reserved)
        {
            int preferredIndex = (int)preferred;
            for (int distance = 0; distance <= 4; distance++)
            {
                int lower = preferredIndex - distance;
                if (lower >= 0 && used.Add((DifficultyTier)lower))
                {
                    reserved = (DifficultyTier)lower;
                    return true;
                }

                int upper = preferredIndex + distance;
                if (distance > 0 && upper <= 4 && used.Add((DifficultyTier)upper))
                {
                    reserved = (DifficultyTier)upper;
                    return true;
                }
            }

            reserved = default;
            return false;
        }

        private static SongCatalogEntry CreateCatalogEntry(string songId, OszBeatmapPackage package,
            IReadOnlyList<PreparedChart> charts, string audioAddress, string jacketAddress, string videoAddress)
        {
            double bpm = charts.Select(chart => chart.Source.Beatmap.Header.Bpm).FirstOrDefault(value => value > 0.0);
            return new SongCatalogEntry
            {
                id = songId,
                title = package.Title,
                artist = package.Artist,
                audioAddress = audioAddress,
                videoAddress = videoAddress,
                jacketAddress = jacketAddress,
                bpm = bpm,
                category = "osu!mania",
                charts = charts.Select(chart => new SongChartEntry
                {
                    difficulty = chart.Source.Beatmap.Header.DifficultyName,
                    tier = chart.Tier.ToString(),
                    level = chart.Level,
                    keyCount = chart.Source.Beatmap.Header.KeyCount,
                    noteCount = chart.Source.Beatmap.Notes.Count,
                    beatmapAddress = chart.Address,
                    coverAddress = jacketAddress
                }).ToArray()
            };
        }

        private static SongCatalogDocument LoadCatalog()
        {
            string sourcePath = File.Exists(ToAbsolute(RemoteCatalogPath)) ? RemoteCatalogPath : BootstrapCatalogPath;
            string json = File.ReadAllText(ToAbsolute(sourcePath), Encoding.UTF8);
            return SongCatalogParser.Parse(json);
        }

        private static void RegisterRemoteAddresses(IReadOnlyDictionary<string, string> addresses)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) throw new OszImportException("Addressables 설정을 만들 수 없습니다.");

            settings.AddLabel(ContentLabel);
            settings.AddLabel(RemoteLabel);
            AddressableAssetGroup group = settings.FindGroup(RemoteGroupName) ?? settings.CreateGroup(
                RemoteGroupName, false, false, false, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));

            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;

            string remoteLoadPath = settings.profileSettings.GetValueByName(
                settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath);
            if (string.IsNullOrWhiteSpace(remoteLoadPath) || remoteLoadPath == "<undefined>")
                settings.profileSettings.SetValue(settings.activeProfileId,
                    AddressableAssetSettings.kRemoteLoadPath, DefaultRemoteLoadPath);

            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            foreach (KeyValuePair<string, string> pair in addresses)
            {
                string guid = AssetDatabase.AssetPathToGUID(pair.Key);
                if (string.IsNullOrEmpty(guid)) throw new OszImportException("Unity asset을 찾을 수 없습니다: " + pair.Key);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = pair.Value;
                entry.SetLabel(ContentLabel, true, true, false);
                entry.SetLabel(RemoteLabel, true, true, false);
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(group);
        }

        private static void ConfigureImporters(string audioPath, string jacketPath, string videoPath)
        {
            if (AssetImporter.GetAtPath(jacketPath) is TextureImporter texture)
            {
                texture.textureType = TextureImporterType.Sprite;
                texture.spriteImportMode = SpriteImportMode.Single;
                texture.mipmapEnabled = false;
                texture.sRGBTexture = true;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.maxTextureSize = 2048;
                texture.SaveAndReimport();
            }

            if (AssetImporter.GetAtPath(audioPath) is AudioImporter audio)
            {
                AudioImporterSampleSettings sampleSettings = audio.defaultSampleSettings;
                sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                sampleSettings.quality = 0.85f;
                sampleSettings.preloadAudioData = true;
                audio.defaultSampleSettings = sampleSettings;
                audio.loadInBackground = false;
                audio.SaveAndReimport();
            }

            if (videoPath != null) AssetDatabase.ImportAsset(videoPath, ImportAssetOptions.ForceUpdate);
        }

        private static string WriteBinary(string folder, string stem, OszBinaryAsset asset)
        {
            string extension = Path.GetExtension(asset.Filename).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
                throw new OszImportException(asset.Filename + " 파일의 확장자를 확인해 주세요.");
            string assetPath = folder + "/" + stem + extension;
            File.WriteAllBytes(ToAbsolute(assetPath), asset.Bytes);
            return assetPath;
        }

        private static void WriteTextAsset(string assetPath, string text) =>
            File.WriteAllText(ToAbsolute(assetPath), text, new UTF8Encoding(false));

        private static string BuildSongId(string oszPath, OszBeatmapPackage package)
        {
            Match setId = Regex.Match(Path.GetFileNameWithoutExtension(oszPath) ?? string.Empty, @"^\s*(\d+)");
            return setId.Success
                ? "osu-" + setId.Groups[1].Value
                : Slug(package.Artist + "-" + package.Title, "imported-song");
        }

        private static string Slug(string value, string fallback)
        {
            string slug = Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? fallback : slug;
        }

        private static string ToAbsolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (projectRoot == null) throw new OszImportException("Unity 프로젝트 경로를 찾을 수 없습니다.");
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent)) throw new OszImportException("폴더 경로가 올바르지 않습니다: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
