using Xunit;
using static QueryLens.Cli.Tests.CliTestHarness;

namespace QueryLens.Cli.Tests
{
    public class VerifyCommandTests
    {
        [Fact]
        public void Passes_when_within_limits()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 110));

            var r = h.Run("verify", current, "--baseline", baseline, "--max-duration-regression", "20", "--max-query-count", "30");

            Assert.Equal(ExitCodes.Success, r.ExitCode);
            Assert.Contains("PASS", r.Output);
        }

        [Fact]
        public void Fails_with_1_when_duration_regression_exceeds_limit()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 150));

            var r = h.Run("verify", current, "--baseline", baseline, "--max-duration-regression", "20");

            Assert.Equal(ExitCodes.VerificationFailed, r.ExitCode);
            Assert.Contains("aaa", r.Output);
            Assert.Contains("50.0%", r.Output);
        }

        [Fact]
        public void Regression_exactly_at_limit_passes()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 120));

            var r = h.Run("verify", current, "--baseline", baseline, "--max-duration-regression", "20");

            Assert.Equal(ExitCodes.Success, r.ExitCode);
        }

        [Fact]
        public void Fails_with_1_when_total_query_count_exceeds_limit()
        {
            using var h = new CliTestHarness();
            var current = h.WriteSnapshot("c.json", Stat("aaa", 5, count: 20), Stat("bbb", 5, count: 15));

            var r = h.Run("verify", current, "--max-query-count", "30");

            Assert.Equal(ExitCodes.VerificationFailed, r.ExitCode);
            Assert.Contains("35", r.Output);
        }

        [Fact]
        public void Query_count_at_limit_passes()
        {
            using var h = new CliTestHarness();
            var current = h.WriteSnapshot("c.json", Stat("aaa", 5, count: 30));

            Assert.Equal(ExitCodes.Success, h.Run("verify", current, "--max-query-count", "30").ExitCode);
        }

        [Fact]
        public void Tiny_baseline_durations_are_ignored_as_noise()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 0.1));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 0.9));

            var r = h.Run("verify", current, "--baseline", baseline, "--max-duration-regression", "20");

            Assert.Equal(ExitCodes.Success, r.ExitCode);
        }

        [Fact]
        public void New_queries_do_not_count_as_duration_regressions()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 100), Stat("zzz", 5000));

            Assert.Equal(ExitCodes.Success,
                h.Run("verify", current, "--baseline", baseline, "--max-duration-regression", "20").ExitCode);
        }

        [Theory]
        [InlineData("verify")]
        [InlineData("verify", "{current}")]
        [InlineData("verify", "{current}", "--max-duration-regression", "20")]
        [InlineData("verify", "{current}", "--max-query-count", "abc")]
        [InlineData("verify", "{current}", "--max-query-count", "-1")]
        [InlineData("verify", "{current}", "--max-query-count", "1.5")]
        [InlineData("verify", "{current}", "--max-query-count")]
        [InlineData("verify", "{current}", "--bogus", "1")]
        [InlineData("verify", "missing-file.json", "--max-query-count", "5")]
        public void Usage_and_input_errors_return_2(params string[] args)
        {
            using var h = new CliTestHarness();
            var current = h.WriteSnapshot("c.json", Stat("aaa", 5));
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "{current}") args[i] = current;
            }

            var r = h.Run(args);

            Assert.Equal(ExitCodes.UsageError, r.ExitCode);
            Assert.NotEmpty(r.Error);
        }

        [Fact]
        public void Malformed_input_returns_2()
        {
            using var h = new CliTestHarness();
            var bad = h.PathFor("bad.json");
            System.IO.File.WriteAllText(bad, "{ not json");

            Assert.Equal(ExitCodes.UsageError, h.Run("verify", bad, "--max-query-count", "5").ExitCode);
        }

        [Fact]
        public void Unknown_command_and_no_args_return_2_and_help_returns_0()
        {
            using var h = new CliTestHarness();

            Assert.Equal(ExitCodes.UsageError, h.Run("frobnicate").ExitCode);
            Assert.Equal(ExitCodes.UsageError, h.Run().ExitCode);
            Assert.Equal(ExitCodes.Success, h.Run("--help").ExitCode);
        }
    }
}
