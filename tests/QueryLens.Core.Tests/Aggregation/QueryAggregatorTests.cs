using System;
using System.Linq;
using QueryLens.Core.Aggregation;
using QueryLens.Core.Models;
using Xunit;

namespace QueryLens.Core.Tests.Aggregation
{
    public class QueryAggregatorTests
    {
        private static QueryExecution Execution(string hash, double durationMs, long? rows = null)
        {
            return new QueryExecution
            {
                QueryHash = hash,
                NormalizedSql = "SELECT * FROM Users WHERE Id = ?",
                RepresentativeSql = "SELECT * FROM Users WHERE Id = 1",
                Duration = TimeSpan.FromMilliseconds(durationMs),
                RowsReturned = rows,
                StartTimeUtc = DateTime.UtcNow
            };
        }

        [Fact]
        public void Add_AccumulatesExecutionCountAndDuration()
        {
            var aggregator = new QueryAggregator();

            aggregator.Add(Execution("HASH1", 100));
            aggregator.Add(Execution("HASH1", 200));
            aggregator.Add(Execution("HASH1", 300));

            var stats = aggregator.GetStatistics("HASH1");

            Assert.Equal(3, stats.ExecutionCount);
            Assert.Equal(100, stats.MinDurationMs);
            Assert.Equal(300, stats.MaxDurationMs);
            Assert.Equal(200, stats.AverageDurationMs);
        }

        [Fact]
        public void GetStatistics_ForUnknownHash_ReturnsNull()
        {
            var aggregator = new QueryAggregator();

            Assert.Null(aggregator.GetStatistics("UNKNOWN"));
        }

        [Fact]
        public void DifferentQueryHashes_AreAggregatedSeparately()
        {
            var aggregator = new QueryAggregator();

            aggregator.Add(Execution("HASH1", 100));
            aggregator.Add(Execution("HASH2", 500));

            var all = aggregator.GetAllStatistics();

            Assert.Equal(2, all.Count);
            Assert.Equal(1, all.Single(s => s.QueryHash == "HASH1").ExecutionCount);
            Assert.Equal(1, all.Single(s => s.QueryHash == "HASH2").ExecutionCount);
        }

        [Fact]
        public void Reset_ClearsAllStatistics()
        {
            var aggregator = new QueryAggregator();
            aggregator.Add(Execution("HASH1", 100));

            aggregator.Reset();

            Assert.Empty(aggregator.GetAllStatistics());
        }

        [Fact]
        public void RowsReturned_TracksMaximum()
        {
            var aggregator = new QueryAggregator();

            aggregator.Add(Execution("HASH1", 100, rows: 10));
            aggregator.Add(Execution("HASH1", 100, rows: 250000));
            aggregator.Add(Execution("HASH1", 100, rows: 50));

            var stats = aggregator.GetStatistics("HASH1");

            Assert.Equal(250000, stats.MaxRowsReturned);
        }

        [Fact]
        public void RowsReturned_WhenNeverProvided_IsNull()
        {
            var aggregator = new QueryAggregator();
            aggregator.Add(Execution("HASH1", 100));

            var stats = aggregator.GetStatistics("HASH1");

            Assert.Null(stats.MaxRowsReturned);
            Assert.Null(stats.AverageRowsReturned);
        }

        [Fact]
        public void Add_WithoutQueryHash_ThrowsArgumentException()
        {
            var aggregator = new QueryAggregator();
            var execution = Execution(null, 100);

            Assert.Throws<ArgumentException>(() => aggregator.Add(execution));
        }

        [Fact]
        public void Percentiles_ReflectTheDistributionOfDurations()
        {
            var aggregator = new QueryAggregator();

            for (var i = 1; i <= 100; i++)
            {
                aggregator.Add(Execution("HASH1", i));
            }

            var stats = aggregator.GetStatistics("HASH1");

            Assert.Equal(100, stats.ExecutionCount);
            Assert.InRange(stats.P50DurationMs, 45, 55);
            Assert.InRange(stats.P99DurationMs, 95, 100);
        }
    }
}
