using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;

namespace QueryLens.Core.Baselines
{
    /// <summary>Pairs baseline and current statistics by QueryHash and computes percentage deltas.</summary>
    public sealed class BaselineComparer
    {
        public BaselineComparison Compare(BaselineSnapshot baseline, IEnumerable<QueryStatistics> current)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));

            var baselineByHash = ToLookup(baseline.Queries);
            var currentByHash = ToLookup(current);
            var items = new List<QueryComparison>();

            foreach (var pair in currentByHash)
            {
                if (baselineByHash.TryGetValue(pair.Key, out var before))
                {
                    items.Add(new QueryComparison
                    {
                        QueryHash = pair.Key,
                        NormalizedSql = pair.Value.NormalizedSql ?? before.NormalizedSql,
                        Status = QueryComparisonStatus.Compared,
                        Baseline = before,
                        Current = pair.Value,
                        AverageDurationChangePercent = ChangePercent(before.AverageDurationMs, pair.Value.AverageDurationMs),
                        P95DurationChangePercent = ChangePercent(before.P95DurationMs, pair.Value.P95DurationMs),
                        CpuChangePercent = ChangePercent(before.AverageCpuMs, pair.Value.AverageCpuMs),
                        LogicalReadsChangePercent = ChangePercent(before.AverageLogicalReads, pair.Value.AverageLogicalReads),
                        ExecutionCountChangePercent = ChangePercent(before.ExecutionCount, pair.Value.ExecutionCount)
                    });
                }
                else
                {
                    items.Add(new QueryComparison
                    {
                        QueryHash = pair.Key,
                        NormalizedSql = pair.Value.NormalizedSql,
                        Status = QueryComparisonStatus.New,
                        Current = pair.Value
                    });
                }
            }

            foreach (var pair in baselineByHash.Where(p => !currentByHash.ContainsKey(p.Key)))
            {
                items.Add(new QueryComparison
                {
                    QueryHash = pair.Key,
                    NormalizedSql = pair.Value.NormalizedSql,
                    Status = QueryComparisonStatus.Removed,
                    Baseline = pair.Value
                });
            }

            return new BaselineComparison(items);
        }

        /// <summary>(current - baseline) / baseline * 100, or null if either side is missing or the baseline is not positive.</summary>
        public static double? ChangePercent(double? baseline, double? current)
        {
            if (!baseline.HasValue || !current.HasValue || baseline.Value <= 0)
            {
                return null;
            }

            return (current.Value - baseline.Value) / baseline.Value * 100.0;
        }

        private static Dictionary<string, QueryStatistics> ToLookup(IEnumerable<QueryStatistics> statistics)
        {
            var result = new Dictionary<string, QueryStatistics>();
            foreach (var stat in statistics ?? Enumerable.Empty<QueryStatistics>())
            {
                if (stat?.QueryHash != null)
                {
                    result[stat.QueryHash] = stat;
                }
            }

            return result;
        }
    }
}
