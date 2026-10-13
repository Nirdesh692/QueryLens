using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Fingerprinting;
using QueryLens.Core.Models;
using QueryLens.SqlServer.Correlation;
using QueryLens.SqlServer.QueryStore;
using Xunit;

namespace QueryLens.SqlServer.Tests.Correlation
{
    public class QueryStoreCorrelatorTests
    {
        private readonly QueryStoreCorrelator _correlator = new QueryStoreCorrelator();
        private readonly SqlQueryFingerprinter _fingerprinter = new SqlQueryFingerprinter();

        private static QueryStatistics Stats(string hash, string sql) =>
            new QueryStatistics { QueryHash = hash, NormalizedSql = sql, ExecutionCount = 1 };

        private static QueryStoreEntry Entry(string sql, long executionCount) =>
            new QueryStoreEntry { QueryText = sql, ExecutionCount = executionCount, QueryId = 1, PlanId = 1 };

        [Fact]
        public void MatchingQuery_CorrelatesBothSides()
        {
            var hash = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE Id = 10").Hash;

            var appStats = new[] { Stats(hash, "SELECT * FROM Orders WHERE Id = ?") };
            var qsEntries = new[] { Entry("SELECT * FROM Orders WHERE Id = 20", 100) }; // different literal, same shape

            var result = _correlator.Correlate(appStats, qsEntries);

            var correlated = Assert.Single(result);
            Assert.True(correlated.HasApplicationData);
            Assert.True(correlated.HasQueryStoreData);
        }

        [Fact]
        public void ApplicationOnlyQuery_HasNoQueryStoreData()
        {
            var result = _correlator.Correlate(
                new[] { Stats("ONLYAPP", "SELECT * FROM Users") },
                Enumerable.Empty<QueryStoreEntry>());

            var correlated = Assert.Single(result);
            Assert.True(correlated.HasApplicationData);
            Assert.False(correlated.HasQueryStoreData);
        }

        [Fact]
        public void QueryStoreOnlyQuery_HasNoApplicationData()
        {
            var result = _correlator.Correlate(
                Enumerable.Empty<QueryStatistics>(),
                new[] { Entry("SELECT * FROM Legacy", 5) });

            var correlated = Assert.Single(result);
            Assert.False(correlated.HasApplicationData);
            Assert.True(correlated.HasQueryStoreData);
        }

        [Fact]
        public void MultiplePlansForSameFingerprint_CollapseToHighestExecutionCountRepresentative()
        {
            var entries = new[]
            {
                Entry("SELECT * FROM Orders WHERE Id = 1", 5),
                Entry("SELECT * FROM Orders WHERE Id = 2", 500)
            };

            var result = _correlator.Correlate(Enumerable.Empty<QueryStatistics>(), entries);

            var correlated = Assert.Single(result);
            Assert.Equal(500L, correlated.QueryStoreEntry.ExecutionCount);
        }

        [Fact]
        public void StructurallyDifferentQueries_DoNotCorrelate()
        {
            var result = _correlator.Correlate(
                new[] { Stats("H1", "SELECT * FROM Users") },
                new[] { Entry("SELECT * FROM Orders", 10) });

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.HasApplicationData && !r.HasQueryStoreData);
            Assert.Contains(result, r => !r.HasApplicationData && r.HasQueryStoreData);
        }
    }
}
