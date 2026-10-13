using System;
using System.Linq;
using System.Threading.Tasks;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;
using Xunit;

namespace QueryLens.Core.Tests.Pipeline
{
    public class QueryLensPipelineTests
    {
        [Fact]
        public async Task CaptureAsync_FingerprintsRawSql()
        {
            var pipeline = new QueryLensPipeline();

            var execution = await pipeline.CaptureAsync(new QueryExecution
            {
                Sql = "SELECT * FROM Users WHERE Id = 42",
                Duration = TimeSpan.FromMilliseconds(10)
            });

            Assert.NotNull(execution.QueryHash);
            Assert.Equal("SELECT * FROM Users WHERE Id = ?", execution.NormalizedSql);
        }

        [Fact]
        public async Task CaptureAsync_WhenDisabled_SkipsAggregation()
        {
            var options = new QueryLensOptions { Enabled = false };
            var pipeline = new QueryLensPipeline(options);

            await pipeline.CaptureAsync(new QueryExecution
            {
                Sql = "SELECT * FROM Users WHERE Id = 42",
                Duration = TimeSpan.FromMilliseconds(10)
            });

            var report = pipeline.Analyze();

            Assert.Empty(report.Statistics);
        }

        [Fact]
        public async Task Analyze_AggregatesRepeatedQueriesAndProducesAScore()
        {
            var pipeline = new QueryLensPipeline();

            for (var i = 0; i < 5; i++)
            {
                await pipeline.CaptureAsync(new QueryExecution
                {
                    Sql = $"SELECT * FROM Users WHERE Id = {i}",
                    Duration = TimeSpan.FromMilliseconds(50),
                    RequestId = "req-1"
                });
            }

            var report = pipeline.Analyze();

            Assert.Single(report.Statistics);
            Assert.Equal(5, report.Statistics.Single().ExecutionCount);
            Assert.InRange(report.Score.Score, 0, 100);
        }

        [Fact]
        public async Task Analyze_FlagsSlowQueryEndToEnd()
        {
            var pipeline = new QueryLensPipeline();

            await pipeline.CaptureAsync(new QueryExecution
            {
                Sql = "SELECT * FROM Orders",
                Duration = TimeSpan.FromMilliseconds(1500)
            });

            var report = pipeline.Analyze();

            Assert.Contains(report.Findings, f => f.RuleId == "CRITICALQUERY001");
            Assert.True(report.Score.Score < 100);
        }

        [Fact]
        public void Options_ExposesTheConfigurationThePipelineWasCreatedWith()
        {
            var options = new QueryLensOptions();
            options.Capture.Parameters = true;

            var pipeline = new QueryLensPipeline(options);

            Assert.Same(options, pipeline.Options);
            Assert.True(pipeline.Options.Capture.Parameters);
        }
    }
}
