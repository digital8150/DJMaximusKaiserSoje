using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using DJMaximusKaiserSoje.Editor;
using NUnit.Framework;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class OszImporterTests
    {
        [Test]
        public void Read_WhenArchiveContainsManiaChart_ResolvesReferencedAssets()
        {
            using (var archive = new TestArchive())
            {
                archive.AddChart("chart.osu", Chart(6, 3, "Normal"));
                archive.AddChart("ignored.osu", Chart(4, 0, "Normal"));
                archive.AddFile("song.ogg", "audio");
                archive.AddFile("cover.jpg", "image");

                OszArchiveContents result = archive.Read();

                Assert.That(result.Songs, Has.Count.EqualTo(1));
                OszBeatmapPackage song = result.Songs[0];
                Assert.That(song.Charts, Has.Count.EqualTo(1));
                Assert.That(song.Charts[0].Beatmap.Header.KeyCount, Is.EqualTo(6));
                Assert.That(song.Title, Is.EqualTo("Test"));
                Assert.That(song.Audio.Filename, Is.EqualTo("song.ogg"));
                Assert.That(song.Jacket.Filename, Is.EqualTo("cover.jpg"));
            }
        }

        [Test]
        public void Read_WhenArchiveHasNoSupportedManiaChart_ReturnsDiagnostic()
        {
            using (var archive = new TestArchive())
            {
                archive.AddChart("chart.osu", Chart(5, 3, "Normal"));

                OszImportException exception = Assert.Throws<OszImportException>(() => archive.Read());

                Assert.That(exception.Message, Does.Contain("4키, 6키 또는 8키"));
            }
        }

        [Test]
        public void Read_WhenArchiveIsAPack_SplitsOneSongPerAudioTrack()
        {
            using (var archive = new TestArchive())
            {
                archive.AddChart("easy.osu", Chart(6, 3, "Persona - AREA 184 (Wolf's 7)", "area.mp3", "area.jpg"));
                archive.AddChart("hard.osu", Chart(6, 3, "Persona - AREA 184 (Wolf's 9)", "area.mp3", "area.jpg", 60));
                archive.AddChart("other.osu", Chart(6, 3, "[10] Rochelle - Mouth", "mouth.mp3", "mouth.png"));
                archive.AddFile("area.mp3", "audio");
                archive.AddFile("area.jpg", "image");
                archive.AddFile("mouth.mp3", "audio");
                archive.AddFile("mouth.png", "image");

                List<OszBeatmapPackage> songs = archive.Read().Songs.OrderBy(song => song.Title).ToList();

                Assert.That(songs.Select(song => song.Title), Is.EqualTo(new[] { "AREA 184", "Mouth" }));
                Assert.That(songs[0].Artist, Is.EqualTo("Persona"));
                Assert.That(songs[0].Charts.Select(chart => chart.DifficultyName),
                    Is.EquivalentTo(new[] { "Wolf's 7", "Wolf's 9" }));
                Assert.That(songs[0].Audio.Filename, Is.EqualTo("area.mp3"));
                Assert.That(songs[1].Artist, Is.EqualTo("Rochelle"));
                Assert.That(songs[1].Charts[0].DifficultyName, Is.EqualTo("Lv.10"));
                Assert.That(songs[1].Jacket.Filename, Is.EqualTo("mouth.png"));
            }
        }

        [Test]
        public void Read_WhenPackTrackIsAPlaceholder_SkipsItAndReportsWhy()
        {
            using (var archive = new TestArchive())
            {
                archive.AddChart("real.osu", Chart(6, 3, "Rochelle - Mouth [10]", "mouth.mp3", "mouth.png"));
                archive.AddChart("also-real.osu", Chart(6, 3, "Lime - Daydreamer [12]", "day.mp3", "day.png"));
                archive.AddChart("filler.osu", Chart(6, 3, "(Not for Play) Welcome!", "delete.mp3", "bg.jpg", 2));
                archive.AddFile("mouth.mp3", "audio");
                archive.AddFile("mouth.png", "image");
                archive.AddFile("day.mp3", "audio");
                archive.AddFile("day.png", "image");
                archive.AddFile("delete.mp3", "audio");
                archive.AddFile("bg.jpg", "image");

                OszArchiveContents result = archive.Read();

                Assert.That(result.Songs.Select(song => song.Title), Is.EquivalentTo(new[] { "Mouth", "Daydreamer" }));
                Assert.That(result.Warnings.Any(warning => warning.Contains("filler.osu")), Is.True);
            }
        }

        [Test]
        public void Read_WhenPackTrackIsMissingItsAudio_KeepsTheRestOfThePack()
        {
            using (var archive = new TestArchive())
            {
                archive.AddChart("real.osu", Chart(6, 3, "Rochelle - Mouth [10]", "mouth.mp3", "mouth.png"));
                archive.AddChart("broken.osu", Chart(6, 3, "Lime - Daydreamer [12]", "gone.mp3", "mouth.png"));
                archive.AddFile("mouth.mp3", "audio");
                archive.AddFile("mouth.png", "image");

                OszArchiveContents result = archive.Read();

                Assert.That(result.Songs.Select(song => song.Title), Is.EqualTo(new[] { "Mouth" }));
                Assert.That(result.Warnings.Any(warning => warning.Contains("gone.mp3")), Is.True);
            }
        }

        [Test]
        public void Read_WhenSingleSongSetHasABracketedTitle_KeepsTheMetadataTitle()
        {
            using (var archive = new TestArchive())
            {
                archive.AddChart("chart.osu", Chart(8, 3, "Mis Fortune's Beginner"));
                archive.AddFile("song.ogg", "audio");
                archive.AddFile("cover.jpg", "image");

                OszBeatmapPackage song = archive.Read().Songs[0];

                Assert.That(song.Title, Is.EqualTo("Test"));
                Assert.That(song.Artist, Is.EqualTo("Artist"));
                Assert.That(song.Charts[0].DifficultyName, Is.EqualTo("Mis Fortune's Beginner"));
            }
        }

        [TestCase("Persona - AREA 184 -Platinum mix- (Imperial Wolf's 7)", "Persona", "AREA 184 -Platinum mix-", "Imperial Wolf's 7")]
        [TestCase("-45 - A c i - L [17]", "-45", "A c i - L", "Lv.17")]
        [TestCase("[9] kozato - D-period -moonlit world line-", "kozato", "D-period -moonlit world line-", "Lv.9")]
        [TestCase("Camellia - Fastest Crash (Cut Ver.) [21]", "Camellia", "Fastest Crash (Cut Ver.)", "Lv.21")]
        [TestCase("Reol - Asymmetry [20] (LN)", "Reol", "Asymmetry", "Lv.20 LN")]
        [TestCase("Vicetone & Tony Igy - Astronomia (Camellia Remix)", "Vicetone & Tony Igy", "Astronomia (Camellia Remix)", null)]
        [TestCase("Normal", null, "Normal", null)]
        public void ParseTrackLabel_SplitsPackConventions(string version, string artist, string title, string difficulty)
        {
            OszTrackLabel label = OszTrackLabel.Parse(version);

            Assert.That(label.Artist, Is.EqualTo(artist));
            Assert.That(label.Title, Is.EqualTo(title));
            Assert.That(label.Difficulty, Is.EqualTo(difficulty));
        }

        [Test]
        public void ReserveSongId_WhenPackTitlesCollide_KeepsEveryIdUnique()
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);

            string first = OszSongId.Reserve("osu-1", "Break", true, taken);
            string second = OszSongId.Reserve("osu-1", "break", true, taken);
            string single = OszSongId.Reserve("osu-2", "Break", false, taken);

            Assert.That(first, Is.EqualTo("osu-1-break"));
            Assert.That(second, Is.EqualTo("osu-1-break-2"));
            Assert.That(single, Is.EqualTo("osu-2"));
        }

        [Test]
        public void ForArchive_TakesTheSetNumberFromTheFilename()
        {
            Assert.That(OszSongId.ForArchive("850880 Various Artists - Pack.osz", "Various Artists", "Pack"),
                Is.EqualTo("osu-850880"));
            Assert.That(OszSongId.ForArchive("hand made.osz", "S3RL", "Pika Girl"),
                Is.EqualTo("s3rl-pika-girl"));
        }

        [Test]
        public void BelongsToSet_MatchesTheSetAndItsPackTracksOnly()
        {
            Assert.That(OszSongId.BelongsToSet("osu-850880", "osu-850880"), Is.True);
            Assert.That(OszSongId.BelongsToSet("osu-850880-candy-galy", "osu-850880"), Is.True);
            Assert.That(OszSongId.BelongsToSet("osu-8508801", "osu-850880"), Is.False);
            Assert.That(OszSongId.BelongsToSet("shooting-star", "osu-850880"), Is.False);
        }

        private static string Chart(int keyCount, int mode, string version, string audio = "song.ogg",
            string background = "cover.jpg", int noteCount = 40)
        {
            var text = new StringBuilder();
            text.Append("osu file format v14\n[General]\nAudioFilename: ").Append(audio)
                .Append("\nMode: ").Append(mode)
                .Append("\n[Metadata]\nTitle:Test\nArtist:Artist\nVersion:").Append(version)
                .Append("\n[Difficulty]\nCircleSize:").Append(keyCount)
                .Append("\n[Events]\n0,0,\"").Append(background)
                .Append("\",0,0\n[TimingPoints]\n0,500,4,2,1,50,1,0\n[HitObjects]\n");
            for (int index = 0; index < noteCount; index++)
                text.Append("64,192,").Append(1000 + index * 250).Append(",1,0,0:0:0:0:\n");
            return text.ToString();
        }

        private sealed class TestArchive : IDisposable
        {
            private readonly string path = Path.Combine(Path.GetTempPath(),
                "djmaximus-osz-test-" + Guid.NewGuid().ToString("N") + ".osz");
            private readonly List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();

            public void AddChart(string name, string text) => AddFile(name, text);

            public void AddFile(string name, string text) =>
                entries.Add(new KeyValuePair<string, string>(name, text));

            public OszArchiveContents Read()
            {
                using (var file = File.Create(path))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    foreach (KeyValuePair<string, string> pair in entries)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(pair.Key);
                        using (Stream stream = entry.Open())
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                            writer.Write(pair.Value);
                    }
                }

                return OszBeatmapPackageReader.Read(path);
            }

            public void Dispose()
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
