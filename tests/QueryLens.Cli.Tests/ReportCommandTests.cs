using System.IO;
using System.Text.Json;
using QueryLens.Cli.Reporting;
using Xunit;
using static QueryLens.Cli.Tests.CliTestHarness;

namespace QueryLens.Cli.Tests
{
    public class ReportCommandTests
    {
        [Fact]
        public void Json_report_has_the_documented_schema()
        {
            using var h = new CliTestHarness();
            var input = h.WriteSnapshot("c.json", Stat("aaa", 2000, count: 7, reads: 42));

            var r = h.Run("report", input, "--format", "json");

            Assert.Equal(ExitCodes.Success, r.ExitCode);
            using var doc = JsonDocument.Parse(r.Output);
            var root = doc.RootElement;
            Assert.Equal(1, root.GetProperty("reportSchemaVersion").GetInt32());
            Assert.Equal("querylens", root.GetProperty("tool").GetString());
            Assert.Equal(FixedNow, root.GetProperty("generatedUtc").GetDateTime().ToUniversalTime());
            Assert.True(root.GetProperty("score").TryGetProperty("value", out _));
            Assert.True(root.GetProperty("score").TryGetProperty("components", out _));

            var query = root.GetProperty("queries")[0];
            Assert.Equal("aaa", query.GetProperty("queryHash").GetString());
            Assert.Equal("SELECT * FROM T_aaa WHERE Id = ?", query.GetProperty("normalizedSql").GetString());
            Assert.Equal(7, query.GetProperty("executionCount").GetInt64());
            Assert.Equal(2000, query.GetProperty("averageDurationMs").GetDouble());
            Assert.Equal(3000, query.GetProperty("p95DurationMs").GetDouble());
            Assert.Equal(42, query.GetProperty("logicalReads").GetDouble());

            var finding = root.GetProperty("findings")[0];
            Assert.False(string.IsNullOrEmpty(finding.GetProperty("ruleId").GetString()));
            Assert.Contains(finding.GetProperty("severity").GetString(), new[] { "Info", "Warning", "Critical" });
            Assert.True(finding.GetProperty("confidence").GetDouble() > 0);
        }

        [Fact]
        public void Json_output_is_deterministic()
        {
            using var h = new CliTestHarness();
            var input = h.WriteSnapshot("c.json", Stat("bbb", 2000), Stat("aaa", 3000));

            Assert.Equal(h.Run("report", input).Output, h.Run("report", input).Output);
        }

        [Fact]
        public void Report_can_be_written_to_file_and_reloaded()
        {
            using var h = new CliTestHarness();
            var input = h.WriteSnapshot("c.json", Stat("aaa", 2000));
            var output = h.PathFor("out.json");

            var r = h.Run("report", input, "--format", "json", "--output", output);

            Assert.Equal(ExitCodes.Success, r.ExitCode);
            var document = ReportSerializer.Deserialize(File.ReadAllText(output));
            Assert.Single(document.Queries);
            Assert.NotEmpty(document.Findings);

            var again = h.Run("report", output);
            Assert.Equal(ExitCodes.Success, again.ExitCode);
            Assert.Equal(ReportSerializer.Serialize(document), again.Output.TrimEnd());
        }

        [Fact]
        public void Html_format_is_a_hook_that_reports_not_available()
        {
            using var h = new CliTestHarness();
            var input = h.WriteSnapshot("c.json", Stat("aaa", 5));

            var r = h.Run("report", input, "--format", "html");

            Assert.Equal(ExitCodes.UsageError, r.ExitCode);
            Assert.Contains("not available yet", r.Error);
        }

        [Fact]
        public void Registered_renderer_is_used_for_its_format()
        {
            using var h = new CliTestHarness();
            var input = h.WriteSnapshot("c.json", Stat("aaa", 5));
            var registry = ReportRendererRegistry.CreateDefault();
            registry.Register(new FakeHtmlRenderer());
            var output = new StringWriter();

            var code = new CliApp(registry).Run(new[] { "report", input, "--format", "html" }, output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("<html>1</html>", output.ToString());
        }

        [Fact]
        public void Unknown_format_returns_2()
        {
            using var h = new CliTestHarness();
            var input = h.WriteSnapshot("c.json", Stat("aaa", 5));

            Assert.Equal(ExitCodes.UsageError, h.Run("report", input, "--format", "pdf").ExitCode);
        }

        [Fact]
        public void Analyze_prints_summary_and_saves_report_with_regression_finding()
        {
            using var h = new CliTestHarness();
            var baseline = h.WriteSnapshot("b.json", Stat("aaa", 100));
            var current = h.WriteSnapshot("c.json", Stat("aaa", 400));
            var output = h.PathFor("report.json");

            var r = h.Run("analyze", current, "--baseline", baseline, "--output", output);

            Assert.Equal(ExitCodes.Success, r.ExitCode);
            Assert.Contains("Performance score:", r.Output);
            Assert.Contains("QUERYREGRESSION001", r.Output);
            Assert.Contains(ReportSerializer.Deserialize(File.ReadAllText(output)).Findings, f => f.RuleId == "QUERYREGRESSION001");
        }

        [Fact]
        public void Newer_report_schema_is_rejected_with_2()
        {
            using var h = new CliTestHarness();
            var path = h.PathFor("future.json");
            File.WriteAllText(path, "{\"reportSchemaVersion\": 99, \"queries\": []}");

            Assert.Equal(ExitCodes.UsageError, h.Run("report", path).ExitCode);
        }

        private sealed class FakeHtmlRenderer : IReportRenderer
        {
            public string Format => "html";

            public string Render(ReportDocument report) => "<html>" + report.Queries.Count + "</html>";
        }
    }
}
