using DJMaximusKaiserSoje.Core;
using NUnit.Framework;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class BackendCoreTests
    {
        [Test]
        public void Score_AllPerfect_ReachesAccuracyAndRatingBoundaries()
        {
            var score = new ScoreAccumulator(3);
            score.Feed(JudgementGrade.PerfectHigh, JudgementTiming.Exact);
            score.Feed(JudgementGrade.PerfectHigh, JudgementTiming.Exact);
            RunScore result = score.Feed(JudgementGrade.PerfectHigh, JudgementTiming.Exact);

            Assert.That(result.Accuracy01, Is.EqualTo(1.0).Within(0.000001));
            Assert.That(result.Rating, Is.EqualTo(100.0).Within(0.000001));
            Assert.That(result.IsAllPerfect, Is.True);
            Assert.That(result.IsFullCombo, Is.True);
        }

        [Test]
        public void Score_AllMiss_IsZeroAndBreaksCombo()
        {
            var score = new ScoreAccumulator(2);
            score.Feed(JudgementGrade.Miss, JudgementTiming.Slow);
            RunScore result = score.Feed(JudgementGrade.Miss, JudgementTiming.Slow);

            Assert.That(result.Score, Is.EqualTo(0));
            Assert.That(result.Accuracy01, Is.EqualTo(0.0));
            Assert.That(result.Rating, Is.EqualTo(0.0));
            Assert.That(result.Combo, Is.EqualTo(0));
            Assert.That(result.Tally.Miss, Is.EqualTo(2));
        }

        [Test]
        public void Score_EmptyChart_DoesNotEarnAccuracyOrRank()
        {
            RunScore result = new ScoreAccumulator(0).Current;

            Assert.That(result.Score, Is.EqualTo(0));
            Assert.That(result.Accuracy01, Is.EqualTo(0.0));
            Assert.That(result.Rating, Is.EqualTo(0.0));
            Assert.That(result.IsAllPerfect, Is.False);
            Assert.That(RankCalculator.FromAccuracy(result.Accuracy01), Is.EqualTo(Rank.F));
        }

        [Test]
        public void Health_MissesEventuallyEmptyTheGauge()
        {
            var rules = new HealthRules();
            HealthState health = rules.StartingHealth;
            for (int index = 0; index < 5; index++) health = rules.Apply(health, JudgementGrade.Miss);

            Assert.That(health.IsEmpty, Is.True);
            Assert.That(rules.HasFailed(health), Is.True);
        }

        [Test]
        public void Sections_ExactBoundaryBelongsToFollowingSection()
        {
            var timeline = new SectionTimeline(1000.0, 4);

            Assert.That(timeline.GetSection(0.0).Index, Is.EqualTo(0));
            Assert.That(timeline.GetSection(249.99).Index, Is.EqualTo(0));
            Assert.That(timeline.GetSection(250.0).Index, Is.EqualTo(1));
            Assert.That(timeline.GetSection(1000.0).Index, Is.EqualTo(3));
            Assert.That(timeline.GetSection(5000.0).Index, Is.EqualTo(3));
        }

        [TestCase(0.99, Rank.SSS)]
        [TestCase(0.98, Rank.SS)]
        [TestCase(0.96, Rank.S)]
        [TestCase(0.93, Rank.A)]
        [TestCase(0.90, Rank.B)]
        [TestCase(0.85, Rank.C)]
        [TestCase(0.80, Rank.D)]
        [TestCase(0.7999, Rank.F)]
        public void Rank_AtThresholds_ReturnsExpectedBadge(double accuracy, Rank expected)
        {
            Assert.That(RankCalculator.FromAccuracy(accuracy), Is.EqualTo(expected));
        }

        [Test]
        public void Preview_WhenUndeclared_UsesFortyPercentFallback()
        {
            var chart = new BeatmapHeader("T", "A", "N", 4, previewTimeMs: -1.0);

            Assert.That(PreviewPointResolver.Resolve(chart, 20000.0), Is.EqualTo(8000.0));
        }

        [Test]
        public void Preview_WhenDeclared_UsesDeclaredPoint()
        {
            var chart = new BeatmapHeader("T", "A", "N", 4, previewTimeMs: 1250.0);

            Assert.That(PreviewPointResolver.Resolve(chart, 20000.0), Is.EqualTo(1250.0));
        }

        [TestCase(PlayStyle.FourKey, 4)]
        [TestCase(PlayStyle.FourKeyFx, 6)]
        [TestCase(PlayStyle.SixKey, 6)]
        [TestCase(PlayStyle.SixKeyFx, 8)]
        public void PlayStyle_RequiresExactChartKeyCount(PlayStyle style, int expectedKeyCount)
        {
            Assert.That(PlayStyleChartCompatibility.RequiredKeyCount(style), Is.EqualTo(expectedKeyCount));
            Assert.That(PlayStyleChartCompatibility.IsCompatible(style,
                new ChartSummary("chart", "song", DifficultyTier.Normal, "Normal", 5, expectedKeyCount, 10)), Is.True);
        }
    }
}
