using System;
using DJMaximusKaiserSoje.Core;
using NUnit.Framework;

namespace DJMaximusKaiserSoje.Tests.EditMode
{
    public sealed class BackendCoreTests
    {
        [Test]
        public void SampleClock_RoundTripsASchedulingTimeWithoutLosingASample()
        {
            const int sampleRate = 48000;
            double scheduled = 12.3456789;

            ulong samples = SampleClock.ToSamples(scheduled, sampleRate);

            Assert.That(samples, Is.EqualTo(592593UL));
            Assert.That(SampleClock.ToSeconds(samples, sampleRate), Is.EqualTo(scheduled).Within(1.0 / sampleRate));
        }

        [Test]
        public void SampleClock_RoundsToNearestSampleSoAStartNeverLandsEarly()
        {
            // 0.5 samples past a boundary: truncating would schedule one sample before the beat.
            Assert.That(SampleClock.ToSamples(1.0 / 48000.0 * 0.5, 48000), Is.EqualTo(1UL));
            Assert.That(SampleClock.ToSamples(1.0 / 48000.0 * 0.49, 48000), Is.EqualTo(0UL));
        }

        [Test]
        public void SampleClock_TimeBeforeTheDeviceStarted_ClampsToZero()
        {
            Assert.That(SampleClock.ToSamples(-3.0, 48000), Is.EqualTo(0UL));
            Assert.That(SampleClock.ToSamples(double.NaN, 48000), Is.EqualTo(0UL));
        }

        [Test]
        public void SampleClock_WhenDeviceReportsNoSampleRate_IsNotSilentlyAccepted()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SampleClock.ToSamples(1.0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SampleClock.ToSeconds(1UL, -1));
        }

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
        public void Health_TenfoldGaugeKeepsExistingDamagePerMiss()
        {
            var rules = new HealthRules();
            HealthState health = rules.StartingHealth;
            for (int index = 0; index < 49; index++) health = rules.Apply(health, JudgementGrade.Miss);

            Assert.That(health.Value, Is.EqualTo(0.2).Within(0.000001));
            Assert.That(health.IsEmpty, Is.False);

            health = rules.Apply(health, JudgementGrade.Miss);
            Assert.That(health.IsEmpty, Is.True);
            Assert.That(rules.HasFailed(health), Is.True);
        }

        [Test]
        public void Health_RecoveryIsTenfoldWhileDamageIsUnchanged()
        {
            var rules = new HealthRules(initialValue: 5.0);

            HealthState recovered = rules.Apply(rules.StartingHealth, JudgementGrade.PerfectHigh);
            HealthState damaged = rules.Apply(recovered, JudgementGrade.Miss);

            Assert.That(HealthState.Maximum, Is.EqualTo(10.0));
            Assert.That(recovered.Value, Is.EqualTo(5.1).Within(0.000001));
            Assert.That(damaged.Value, Is.EqualTo(4.9).Within(0.000001));
            Assert.That(HealthRules.MissDelta, Is.EqualTo(-0.2));
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

        [TestCase(64, 64)]
        [TestCase(40, 64)]
        [TestCase(100, 128)]
        [TestCase(300, 256)]
        [TestCase(900, 1024)]
        [TestCase(4096, 1024)]
        public void Options_AudioBufferUsesNearestSupportedSize(int requested, int expected)
        {
            Assert.That(GameOptionRules.NormalizeAudioBufferSize(requested), Is.EqualTo(expected));
        }

        [Test]
        public void Options_RebindingUsedKeySwapsLanesAndKeepsBindingsUnique()
        {
            string[] defaults = GameOptionRules.DefaultBindings(PlayStyle.FourKey);

            string[] result = GameOptionRules.Rebind(PlayStyle.FourKey, defaults, 0, defaults[1]);

            Assert.That(result, Is.EqualTo(new[] { "F", "D", "J", "K" }));
        }

        [Test]
        public void Options_MalformedBindingListFallsBackToModeDefaults()
        {
            string[] result = GameOptionRules.NormalizeBindings(PlayStyle.SixKeyFx,
                new[] { "A", "A" });

            Assert.That(result, Is.EqualTo(GameOptionRules.DefaultBindings(PlayStyle.SixKeyFx)));
        }

        [Test]
        public void Options_JudgementOffsetStepsAndClampsToSupportedRange()
        {
            Assert.That(JudgementOffsetRange.Stepped(-110.0, 1), Is.EqualTo(-105.0));
            Assert.That(JudgementOffsetRange.Stepped(JudgementOffsetRange.MaximumMs, 1),
                Is.EqualTo(JudgementOffsetRange.MaximumMs));
            Assert.That(JudgementOffsetRange.Stepped(JudgementOffsetRange.MinimumMs, -1),
                Is.EqualTo(JudgementOffsetRange.MinimumMs));
        }

        [TestCase(PlayStyle.FourKeyFx, 0, LaneRole.LeftFx)]
        [TestCase(PlayStyle.FourKeyFx, 5, LaneRole.RightFx)]
        [TestCase(PlayStyle.SixKeyFx, 0, LaneRole.LeftFx)]
        [TestCase(PlayStyle.SixKeyFx, 7, LaneRole.RightFx)]
        public void FxPlayStyle_IdentifiesLeftAndRightBindings(PlayStyle style, int lane, LaneRole expected)
        {
            Assert.That(LaneLayout.Create(style).Lanes[lane].Role, Is.EqualTo(expected));
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
