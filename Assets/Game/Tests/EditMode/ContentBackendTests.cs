using DJMaximusKaiserSoje.Content;
using DJMaximusKaiserSoje.Core;
using NUnit.Framework;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class ContentBackendTests
    {
        private const string Chart = "[General]\nAudioFilename: song.ogg\nPreviewTime:1234\n[Metadata]\nTitle:Test Song\nArtist:Test Artist\nVersion:Hard\n[Difficulty]\nCircleSize:6\n[TimingPoints]\n0,500,4,2,1,50,1,0\n1000,-100,4,2,1,50,0,0\n[Events]\nVideo,250,\"movie.mp4\"\n0,0,\"cover.jpg\",0,0\n[HitObjects]\n0,192,1000,1,0,0:0:0:0:\n511,192,1500,128,0,2200:0:0:0:0:";

        [Test]
        public void Parser_MapsMetadataPreviewBpmAndObjects()
        {
            Beatmap result = new OsuManiaBeatmapParser().Parse(Chart);

            Assert.That(result.Header.Title, Is.EqualTo("Test Song"));
            Assert.That(result.Header.KeyCount, Is.EqualTo(6));
            Assert.That(result.Header.PreviewTimeMs, Is.EqualTo(1234.0));
            Assert.That(result.Header.Bpm, Is.EqualTo(120.0).Within(0.0001));
            Assert.That(result.Header.VideoFilename, Is.EqualTo("movie.mp4"));
            Assert.That(result.Header.AudioFilename, Is.EqualTo("song.ogg"));
            Assert.That(result.Notes, Has.Count.EqualTo(2));
            Assert.That(result.Notes[1].EndTimeMs, Is.EqualTo(2200.0));
        }

        [Test]
        public void Parser_WhenHoldIsMalformed_ReturnsDiagnostic()
        {
            const string malformed = "[Difficulty]\nCircleSize:4\n[HitObjects]\n64,192,1000,128,0,900:0:0:0:0:";

            BeatmapParseException exception = Assert.Throws<BeatmapParseException>(() => new OsuManiaBeatmapParser().Parse(malformed));

            Assert.That(exception.Message, Does.Contain("invalid end time"));
        }

        [Test]
        public void Catalog_V1_IsMigratedWithTierAndLevelDefaults()
        {
            const string json = "{\"schemaVersion\":1,\"songs\":[{\"id\":\"demo\",\"title\":\"Demo\",\"artist\":\"Artist\",\"audioAddress\":\"audio\",\"charts\":[{\"difficulty\":\"Insane\",\"beatmapAddress\":\"map\",\"coverAddress\":\"cover\"}]}]}";

            SongCatalogDocument result = SongCatalogParser.Parse(json);

            Assert.That(result.SchemaVersion, Is.EqualTo(2));
            Assert.That(result.OriginalSchemaVersion, Is.EqualTo(1));
            Assert.That(result.WasMigrated, Is.True);
            Assert.That(result.songs[0].charts[0].tier, Is.EqualTo("SuperHard"));
            Assert.That(result.songs[0].charts[0].level, Is.EqualTo(15));
            Assert.That(result.songs[0].charts[0].keyCount, Is.EqualTo(4));
            Assert.That(result.songs[0].category, Is.EqualTo("Prototype"));
        }

        [Test]
        public void Catalog_V2_PreservesSongAndChartMetadataAndStableIds()
        {
            const string json = "{\"schemaVersion\":2,\"songs\":[{\"id\":\"demo\",\"title\":\"Demo\",\"artist\":\"Artist\",\"audioAddress\":\"audio\",\"bpm\":180,\"category\":\"Pack\",\"charts\":[{\"difficulty\":\"HD\",\"tier\":\"Hard\",\"level\":12,\"keyCount\":4,\"noteCount\":42,\"beatmapAddress\":\"map\",\"coverAddress\":\"cover\"}]}]}";

            var library = new CatalogSongLibrary(SongCatalogParser.Parse(json));

            Assert.That(library.Songs[0].Bpm, Is.EqualTo(180.0));
            Assert.That(library.Songs[0].Category, Is.EqualTo("Pack"));
            Assert.That(library.TryGetChart("demo.hard", out ChartSummary chart), Is.True);
            Assert.That(chart.Level, Is.EqualTo(12));
            Assert.That(chart.NoteCount, Is.EqualTo(42));
            Assert.That(library.GetContent("demo.hard").ChartAddress, Is.EqualTo("map"));
        }

        [Test]
        public void Catalog_UnknownVersion_IsNotSilentlyAccepted()
        {
            const string json = "{\"schemaVersion\":99,\"songs\":[]}";

            Assert.Throws<SongCatalogException>(() => SongCatalogParser.Parse(json));
        }

        [Test]
        public void Catalog_SameTierAtDifferentKeyCounts_RemainsSelectableByStyle()
        {
            const string json = "{\"schemaVersion\":2,\"songs\":[{\"id\":\"multi\",\"title\":\"Multi\",\"artist\":\"Artist\",\"audioAddress\":\"audio\",\"jacketAddress\":\"cover\",\"charts\":[{\"difficulty\":\"Normal 4K\",\"tier\":\"Normal\",\"level\":5,\"keyCount\":4,\"noteCount\":10,\"beatmapAddress\":\"map4\"},{\"difficulty\":\"Normal 6K\",\"tier\":\"Normal\",\"level\":5,\"keyCount\":6,\"noteCount\":12,\"beatmapAddress\":\"map6\"}]}]}";
            var library = new CatalogSongLibrary(SongCatalogParser.Parse(json));
            SongSummary song = library.Songs[0];

            Assert.That(song.TryGetChart(DifficultyTier.Normal, PlayStyle.FourKey, out ChartSummary fourKey), Is.True);
            Assert.That(fourKey.KeyCount, Is.EqualTo(4));
            Assert.That(song.TryGetChart(DifficultyTier.Normal, PlayStyle.SixKey, out ChartSummary sixKey), Is.True);
            Assert.That(sixKey.KeyCount, Is.EqualTo(6));
            Assert.That(fourKey.Id, Is.Not.EqualTo(sixKey.Id));
        }
    }
}
