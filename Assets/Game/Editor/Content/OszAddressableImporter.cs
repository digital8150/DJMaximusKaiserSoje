using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    /// Imports osu!mania sets into project assets, updates the versioned remote catalog, assigns
    /// stable Addressables keys, and builds player content once for the whole batch. Imported files
    /// remain ordinary Unity assets so the generated catalog can be rebuilt or uploaded by the
    /// release pipeline. A pack archive lands as one song per track, not as a single entry.
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

        /// <summary>
        /// Song audio ships as the original encoded file so the mixer decodes it. Appending this to
        /// the real extension keeps the format readable (audio.mp3.bytes) while telling Unity to
        /// import the file as raw data instead of turning it into an AudioClip.
        /// </summary>
        private const string RawAudioExtension = ".bytes";

        private sealed class PreparedChart
        {
            public OszChartSource Source;
            public DifficultyTier Tier;
            public int Level;
            public string Address;
        }

        private sealed class ImportedSong
        {
            public SongCatalogEntry Entry;
            public string JacketPath;
            public string VideoPath;
        }

        [MenuItem("Tools/DJ Maximus/Import osu!mania .osz")]
        public static void ImportWithPicker()
        {
            string path = EditorUtility.OpenFilePanel("osu!mania 곡 가져오기", string.Empty, "osz");
            if (string.IsNullOrEmpty(path)) return;
            RunImport(new[] { path });
        }

        [MenuItem("Tools/DJ Maximus/Import osu!mania .osz folder")]
        public static void ImportFolderWithPicker()
        {
            string folder = EditorUtility.OpenFolderPanel("osu!mania 곡 폴더 가져오기", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(folder)) return;

            string[] paths = Directory.GetFiles(folder, "*.osz", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("가져올 곡이 없어요", "이 폴더에 .osz 파일이 없습니다.", "확인");
                return;
            }

            RunImport(paths);
        }

        private static void RunImport(IReadOnlyList<string> oszPaths)
        {
            try
            {
                IReadOnlyList<string> songIds = Import(oszPaths);
                EditorUtility.DisplayDialog("가져오기 완료",
                    "곡 " + songIds.Count + "개를 만들었습니다.\n\n출력: ServerData", "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("가져오지 못했어요", exception.Message, "확인");
            }
        }

        public static string ImportAndBuild(string oszPath) => Import(new[] { oszPath }).First();

        /// <summary>
        /// Imports every archive, rewrites the remote catalog, and builds Addressables content once.
        /// Archives are read up front so a broken file fails before anything is written.
        /// </summary>
        public static IReadOnlyList<string> Import(IReadOnlyList<string> oszPaths)
        {
            if (oszPaths == null || oszPaths.Count == 0)
                throw new OszImportException("가져올 .osz 파일이 없습니다.");

            var archives = new List<KeyValuePair<string, OszArchiveContents>>(oszPaths.Count);
            foreach (string oszPath in oszPaths)
            {
                if (!string.Equals(Path.GetExtension(oszPath), ".osz", StringComparison.OrdinalIgnoreCase))
                    throw new OszImportException(".osz 파일을 선택해 주세요: " + oszPath);

                OszArchiveContents contents = OszBeatmapPackageReader.Read(oszPath);
                foreach (string warning in contents.Warnings)
                    Debug.LogWarning(Path.GetFileName(oszPath) + " — " + warning);
                OszBeatmapPackage first = contents.Songs[0];
                archives.Add(new KeyValuePair<string, OszArchiveContents>(
                    OszSongId.ForArchive(oszPath, first.Artist, first.Title), contents));
            }

            SongCatalogDocument catalog = LoadCatalog();
            var addresses = new Dictionary<string, string>(StringComparer.Ordinal);
            List<SongCatalogEntry> kept = catalog.Songs
                .Where(song => song != null && !archives.Any(archive => OszSongId.BelongsToSet(song.id, archive.Key)))
                .ToList();
            var songIds = new HashSet<string>(kept.Select(song => song.id), StringComparer.Ordinal);
            var imported = new List<ImportedSong>();

            EnsureFolder(ImportRoot);
            foreach (KeyValuePair<string, OszArchiveContents> archive in archives)
            {
                RemoveSetFolders(archive.Key);
                bool isPack = archive.Value.Songs.Count > 1;
                foreach (OszBeatmapPackage song in archive.Value.Songs)
                    imported.Add(WriteSong(
                        OszSongId.Reserve(archive.Key, song.Title, isPack, songIds), song, addresses));
            }

            catalog.songs = kept.Concat(imported.Select(song => song.Entry)).ToArray();
            catalog.schemaVersion = SongCatalogParser.SupportedSchemaVersion;
            WriteTextAsset(RemoteCatalogPath, JsonUtility.ToJson(catalog, true) + Environment.NewLine);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (ImportedSong song in imported) ConfigureImporters(song.JacketPath, song.VideoPath);
            addresses.Add(RemoteCatalogPath, RemoteCatalogAddress);
            RegisterRemoteAddresses(addresses);
            AssetDatabase.SaveAssets();

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrWhiteSpace(result.Error))
                throw new OszImportException("Addressables 빌드에 실패했습니다: " + result.Error);

            Debug.Log("Imported " + imported.Count + " songs from " + archives.Count +
                      " archives and built Addressables content.");
            return imported.Select(song => song.Entry.id).ToArray();
        }

        private static ImportedSong WriteSong(string songId, OszBeatmapPackage package,
            IDictionary<string, string> addresses)
        {
            string songFolder = ImportRoot + "/" + songId;
            EnsureFolder(songFolder);

            string addressPrefix = "song." + songId;
            string audioPath = WriteBinary(songFolder, "audio", package.Audio, RawAudioExtension);
            string jacketPath = WriteBinary(songFolder, "jacket", package.Jacket);
            string videoPath = package.Video == null ? null : WriteBinary(songFolder, "video", package.Video);
            addresses.Add(audioPath, addressPrefix + ".audio");
            addresses.Add(jacketPath, addressPrefix + ".jacket");
            if (videoPath != null) addresses.Add(videoPath, addressPrefix + ".video");

            List<PreparedChart> charts = PrepareCharts(package, songFolder, addressPrefix, addresses);
            if (charts.Count == 0) throw new OszImportException(package.Title + ": 가져올 수 있는 난이도가 없습니다.");

            return new ImportedSong
            {
                Entry = CreateCatalogEntry(songId, package, charts, addresses[audioPath], addresses[jacketPath],
                    videoPath == null ? string.Empty : addresses[videoPath]),
                JacketPath = jacketPath,
                VideoPath = videoPath
            };
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
                    DifficultyTier inferred = SongCatalogParser.InferTier(source.DifficultyName);
                    if (!TryReserveTier(inferred, usedTiers, out DifficultyTier tier))
                    {
                        Debug.LogWarning("Skipped extra " + group.Key + "K chart: " + package.Title + " / " +
                                         source.DifficultyName);
                        continue;
                    }

                    int level = SongCatalogParser.InferLevel(source.DifficultyName, tier);
                    string chartSlug = OszSongId.Slug(source.DifficultyName, "chart") + "-" + (++nameIndex);
                    string assetPath = songFolder + "/" + group.Key + "k-" + chartSlug + ".txt";
                    string address = addressPrefix + "." + group.Key + "k." + chartSlug + ".beatmap";
                    WriteTextAsset(assetPath, source.Text);
                    addresses.Add(assetPath, address);
                    result.Add(new PreparedChart
                    {
                        Source = source,
                        Tier = tier,
                        Level = level,
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
                    difficulty = string.IsNullOrWhiteSpace(chart.Source.DifficultyName)
                        ? chart.Tier.ToString()
                        : chart.Source.DifficultyName,
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
            // One bundle per asset: a player that picks a song downloads that song, not the library.
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;

            string remoteLoadPath = settings.profileSettings.GetValueByName(
                settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath);
            if (string.IsNullOrWhiteSpace(remoteLoadPath) || remoteLoadPath == "<undefined>")
                settings.profileSettings.SetValue(settings.activeProfileId,
                    AddressableAssetSettings.kRemoteLoadPath, DefaultRemoteLoadPath);

            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            // Entries whose asset is gone would otherwise keep a stale address alive in the catalog.
            foreach (AddressableAssetEntry stale in group.entries.ToArray())
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(stale.guid);
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(ToAbsolute(assetPath)))
                    settings.RemoveAssetEntry(stale.guid, false);
            }

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

        private static void ConfigureImporters(string jacketPath, string videoPath)
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

            if (videoPath != null) AssetDatabase.ImportAsset(videoPath, ImportAssetOptions.ForceUpdate);
        }

        private static string WriteBinary(string folder, string stem, OszBinaryAsset asset, string extraExtension = "")
        {
            string extension = Path.GetExtension(asset.Filename).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
                throw new OszImportException(asset.Filename + " 파일의 확장자를 확인해 주세요.");
            string assetPath = folder + "/" + stem + extension + extraExtension;
            File.WriteAllBytes(ToAbsolute(assetPath), asset.Bytes);
            return assetPath;
        }

        private static void WriteTextAsset(string assetPath, string text) =>
            File.WriteAllText(ToAbsolute(assetPath), text, new UTF8Encoding(false));

        /// <summary>Drops what a previous import of the same set left behind, including its songs.</summary>
        private static void RemoveSetFolders(string setId)
        {
            foreach (string folder in AssetDatabase.GetSubFolders(ImportRoot))
            {
                if (!OszSongId.BelongsToSet(Path.GetFileName(folder), setId)) continue;
                if (!AssetDatabase.DeleteAsset(folder))
                    throw new OszImportException("기존 가져오기 폴더를 갱신할 수 없습니다: " + folder);
            }
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
