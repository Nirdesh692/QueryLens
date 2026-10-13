using System.Linq;
using QueryLens.Core.Baselines;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using Xunit;

namespace QueryLens.Core.Tests.Baselines
{
    public class BaselineTests
    {
        private static QueryStatistics Stat(string hash, double avg, double? cpu = null, double? reads = null, long count = 10)
        {
            return new QueryStatistics
            {
                QueryHash = hash,
                NormalizedSql = "SELECT " + hash,
                AverageDurationMs = avg,
                P95DurationMs = avg * 2,
                AverageCpuMs = cpu,
                AverageLogicalReads = reads,
                ExecutionCount = count
            };
        }

        private static BaselineComparison Compare(QueryStatistics before, QueryStatistics after)
        {
            var baseline = BaselineSnapshot.FromStatistics(new[] { before }, "v1");
            return new BaselineComparer().Compare(baseline, new[] { after });
        }

        [Fact]
        public void Serializer_RoundTripsSnapshot()
        {
            var snapshot = BaselineSnapshot.FromStatistics(new[] { Stat("a", 12.5, 3, 100) }, "release-1");

            var restored = BaselineSerializer.Deserialize(BaselineSerializer.Serialize(snapshot));

            Assert.Equal("release-1", restored.Label);
            var q = Assert.Single(restored.Queries);
            Assert.Equal("a", q.QueryHash);
            Assert.Equal(12.5, q.AverageDurationMs);
            Assert.Equal(3, q.AverageCpuMs);
        }

        [Fact]
        public void Serializer_RejectsNewerSchemaVersion()
        {
            var json = "{\"schemaVersion\": 999, \"queries\": []}";
            Assert.Throws<System.IO.InvalidDataException>(() => BaselineSerializer.Deserialize(json));
        }

        [Fact]
        public void Serializer_RejectsMalformedJson()
        {
            Assert.Throws<System.IO.InvalidDataException>(() => BaselineSerializer.Deserialize("{ not json"));
        }

        [Fact]
        public void Comparer_ComputesPercentChange()
        {
            var comparison = Compare(Stat("a", 100, 10, 1000), Stat("a", 150, 10, 2000));

            var item = Assert.Single(comparison.Items);
            Assert.Equal(QueryComparisonStatus.Compared, item.Status);
            Assert.Equal(50, item.AverageDurationChangePercent.Value, 6);
            Assert.Equal(0, item.CpuChangePercent.Value, 6);
            Assert.Equal(100, item.LogicalReadsChangePercent.Value, 6);
        }

        [Fact]
        public void Comparer_ReportsNewAndRemovedQueries()
        {
            var baseline = BaselineSnapshot.FromStatistics(new[] { Stat("old", 10) });
            var comparison = new BaselineComparer().Compare(baseline, new[] { Stat("new", 10) });

            Assert.Contains(comparison.Items, i => i.QueryHash == "new" && i.Status == QueryComparisonStatus.New);
            Assert.Contains(comparison.Items, i => i.QueryHash == "old" && i.Status == QueryComparisonStatus.Removed);
        }

        [Fact]
        public void ChangePercent_ReturnsNullWhenBaselineMissingOrZero()
        {
            Assert.Null(BaselineComparer.ChangePercent(null, 5));
            Assert.Null(BaselineComparer.ChangePercent(0, 5));
            Assert.Null(BaselineComparer.ChangePercent(5, null));
        }

        [Fact]
        public void Rule_FlagsRegressionBeyondTolerance_AsWarning()
        {
            var findings = new QueryRegressionRule().Evaluate(Compare(Stat("a", 100), Stat("a", 130)), new QueryLensOptions());

            var f = Assert.Single(findings);
            Assert.Equal("QUERYREGRESSION001", f.RuleId);
            Assert.Equal(Severity.Warning, f.Severity);
            Assert.Equal("a", f.QueryHash);
        }

        [Fact]
        public void Rule_FlagsLargeRegression_AsCritical()
        {
            var findings = new QueryRegressionRule().Evaluate(Compare(Stat("a", 100), Stat("a", 250)), new QueryLensOptions());

            Assert.Equal(Severity.Critical, Assert.Single(findings).Severity);
        }

        [Fact]
        public void Rule_IgnoresChangeWithinToleranceAndImprovements()
        {
            var options = new QueryLensOptions();
            var rule = new QueryRegressionRule();

            Assert.Empty(rule.Evaluate(Compare(Stat("a", 100), Stat("a", 110)), options));
            Assert.Empty(rule.Evaluate(Compare(Stat("a", 100), Stat("a", 40)), options));
        }

        [Fact]
        public void Rule_IgnoresNoiseBelowMinimumBaseline()
        {
            var findings = new QueryRegressionRule().Evaluate(Compare(Stat("a", 0.2), Stat("a", 0.9)), new QueryLensOptions());

            Assert.Empty(findings);
        }

        [Fact]
        public void Rule_DetectsLogicalReadsRegression()
        {
            var findings = new QueryRegressionRule().Evaluate(
                Compare(Stat("a", 100, null, 1000), Stat("a", 100, null, 3000)), new QueryLensOptions());

            Assert.Single(findings);
        }

        [Fact]
        public void Rule_RespectsDisabledOption()
        {
            var options = new QueryLensOptions();
            options.Analysis.QueryRegression = false;

            Assert.Empty(new QueryRegressionRule().Evaluate(Compare(Stat("a", 100), Stat("a", 500)), options));
        }

        [Fact]
        public async System.Threading.Tasks.Task Pipeline_AnalyzeAgainstBaseline_AddsRegressionFindingAndLowersScore()
        {
            var fast = new Core.Pipeline.QueryLensPipeline();
            var slow = new Core.Pipeline.QueryLensPipeline();
            await fast.CaptureAsync(new QueryExecution { Sql = "SELECT * FROM Users WHERE Id = 1", Duration = System.TimeSpan.FromMilliseconds(10) });
            await slow.CaptureAsync(new QueryExecution { Sql = "SELECT * FROM Users WHERE Id = 2", Duration = System.TimeSpan.FromMilliseconds(50) });

            var baseline = fast.CreateBaseline("v1");
            var plain = slow.Analyze();
            var report = slow.AnalyzeAgainstBaseline(baseline);

            Assert.Contains(report.Findings, f => f.RuleId == "QUERYREGRESSION001");
            Assert.True(report.Score.Score < plain.Score.Score);
        }
    }
}
