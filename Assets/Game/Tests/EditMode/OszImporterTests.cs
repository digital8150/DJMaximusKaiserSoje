using System;
using System.IO;
using System.IO.Compression;
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
            string path = Path.Combine(Path.GetTempPath(), "djmaximus-osz-test-" + Guid.NewGuid().ToString("N") + ".osz");
            try
            {
                using (var file = File.Create(path))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    Write(archive, "chart.osu", Chart(6, 3));
                    Write(archive, "ignored.osu", Chart(4, 0));
                    Write(archive, "song.ogg", "audio");
                    Write(archive, "cover.jpg", "image");
                }

                OszBeatmapPackage result = OszBeatmapPackageReader.Read(path);

                Assert.That(result.Charts, Has.Count.EqualTo(1));
                Assert.That(result.Charts[0].Beatmap.Header.KeyCount, Is.EqualTo(6));
                Assert.That(result.Audio.Filename, Is.EqualTo("song.ogg"));
                Assert.That(result.Jacket.Filename, Is.EqualTo("cover.jpg"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Read_WhenArchiveHasNoSupportedManiaChart_ReturnsDiagnostic()
        {
            string path = Path.Combine(Path.GetTempPath(), "djmaximus-osz-test-" + Guid.NewGuid().ToString("N") + ".osz");
            try
            {
                using (var file = File.Create(path))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
                    Write(archive, "chart.osu", Chart(5, 3));

                OszImportException exception = Assert.Throws<OszImportException>(() =>
                    OszBeatmapPackageReader.Read(path));

                Assert.That(exception.Message, Does.Contain("4키, 6키 또는 8키"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static string Chart(int keyCount, int mode) =>
            "osu file format v14\n[General]\nAudioFilename: song.ogg\nMode: " + mode +
            "\n[Metadata]\nTitle:Test\nArtist:Artist\nVersion:Normal\n[Difficulty]\nCircleSize:" + keyCount +
            "\n[Events]\n0,0,\"cover.jpg\",0,0\n[TimingPoints]\n0,500,4,2,1,50,1,0\n[HitObjects]\n64,192,1000,1,0,0:0:0:0:";

        private static void Write(ZipArchive archive, string name, string value)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using (Stream stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(value);
        }
    }
}
