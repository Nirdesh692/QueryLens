using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Fingerprinting;
using QueryLens.Core.Models;
using QueryLens.SqlServer.QueryStore;

namespace QueryLens.SqlServer.Correlation
{
    public interface IQueryStoreCorrelator
    {
        IReadOnlyList<CorrelatedQuery> Correlate(
            IEnumerable<QueryStatistics> applicationStatistics,
            IEnumerable<QueryStoreEntry> queryStoreEntries);
    }

    /// <summary>
    /// Correlates application-captured queries with Query Store entries.
    ///
    /// QueryLens's own QueryHash and SQL Server's internal query_hash are
    /// computed by entirely different algorithms over potentially different
    /// text - they must never be assumed to match. Instead, this re-runs
    /// QueryLens's own fingerprinter over the SQL text Query Store captured,
    /// producing a QueryHash directly comparable to the ones already attached
    /// to application-side QueryStatistics. This is a best-effort strategy,
    /// not a guarantee: it works well when the text SQL Server stored is the
    /// same text the application sent (the common case), and can miss when a
    /// provider rewrites text on its way to the server.
    /// </summary>
    public sealed class QueryStoreCorrelator : IQueryStoreCorrelator
    {
        private readonly IQueryFingerprinter _fingerprinter;

        public QueryStoreCorrelator(IQueryFingerprinter fingerprinter = null)
        {
            _fingerprinter = fingerprinter ?? new SqlQueryFingerprinter();
        }

        public IReadOnlyList<CorrelatedQuery> Correlate(
            IEnumerable<QueryStatistics> applicationStatistics,
            IEnumerable<QueryStoreEntry> queryStoreEntries)
        {
            var results = new Dictionary<string, CorrelatedQuery>();

            foreach (var entry in queryStoreEntries ?? Enumerable.Empty<QueryStoreEntry>())
            {
                var hash = _fingerprinter.Fingerprint(entry.QueryText ?? string.Empty).Hash;

                if (!results.TryGetValue(hash, out var correlated))
                {
                    correlated = new CorrelatedQuery { QueryHash = hash };
                    results[hash] = correlated;
                }

                // Multiple Query Store plans (recompiles, parameter sniffing,
                // etc.) can map to the same fingerprint; keep the one with the
                // most executions as representative.
                if (correlated.QueryStoreEntry == null || entry.ExecutionCount > correlated.QueryStoreEntry.ExecutionCount)
                {
                    correlated.QueryStoreEntry = entry;
                }
            }

            foreach (var stats in applicationStatistics ?? Enumerable.Empty<QueryStatistics>())
            {
                if (!results.TryGetValue(stats.QueryHash, out var correlated))
                {
                    correlated = new CorrelatedQuery { QueryHash = stats.QueryHash };
                    results[stats.QueryHash] = correlated;
                }

                correlated.ApplicationStatistics = stats;
            }

            return results.Values.ToList();
        }
    }
}
