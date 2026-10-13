using System.IO;
using Xunit;
using static QueryLens.Cli.Tests.CliTestHarness;

namespace QueryLens.Cli.Tests
{
    public class CompareCommandTests
    {
        [Fact]
        public void Reports_percentage_changes_new_and_removed_queries()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100), Stat("old", 10));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 150), Stat("new", 20));

            var r = h.Run("compare", baseline, current);

            Assert.Equal(ExitCodes.Success, r.ExitCode);
            Assert.Contains("Compared: 1, New: 1, Removed: 1", r.Output);
            Assert.Contains("aaa  avg +50.0%  p95 +50.0%", r.Output);
            Assert.Contains("New queries:", r.Output);
            Assert.Contains("new  ", r.Output);
            Assert.Contains("Removed queries:", r.Output);
            Assert.Contains("old  ", r.Output);
        }

        [Fact]
        public void Shows_decreases_with_minus_sign_and_missing_metrics_as_na()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 200));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 100));

            var r = h.Run("compare", baseline, current);

            Assert.Contains("avg -50.0%", r.Output);
            Assert.Contains("reads n/a", r.Output);
        }

        [Fact]
        public void Orders_worst_regression_first()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100), Stat("bbb", 100));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 110), Stat("bbb", 300));

            var output = h.Run("compare", baseline, current).Output;

            Assert.True(output.IndexOf("bbb  avg") < output.IndexOf("aaa  avg"));
        }

        [Fact]
        public void Accepts_report_json_as_well_as_snapshots()
        {
            using var h = new CliTestHarness();
            var snapshot = h.WriteSnapshot("s.json", Stat("aaa", 100));
            var reportPath = h.PathFor("r.json");
            Assert.Equal(0, h.Run("analyze", h.WriteSnapshot("c.json", Stat("aaa", 300)), "--output", reportPath).ExitCode);

            var r = h.Run("compare", snapshot, reportPath);

            Assert.Equal(ExitCodes.Success, r.ExitCode);
            Assert.Contains("avg +200.0%", r.Output);
        }

        [Fact]
        public void Wrong_argument_count_returns_2()
        {
            using var h = new CliTestHarness();
            var b = h.WriteSnapshot("b.json", Stat("aaa", 1));

            Assert.Equal(ExitCodes.UsageError, h.Run("compare", b).ExitCode);
            Assert.Equal(ExitCodes.UsageError, h.Run("compare", b, h.PathFor("nope.json")).ExitCode);
        }
    }
}
