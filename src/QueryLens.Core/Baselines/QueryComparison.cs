using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Baselines
{
    public enum QueryComparisonStatus
    {
        /// <summary>Present in both baseline and current run.</summary>
        Compared,

        /// <summary>Only in the current run.</summary>
        New,

        /// <summary>Only in the baseline.</summary>
        Removed
    }

    /// <summary>Baseline-vs-current comparison for one query. Change values are percentages; null when not computable.</summary>
    public sealed class QueryComparison
    {
        public string QueryHash { get; set; }

        public string NormalizedSql { get; set; }

        public QueryComparisonStatus Status { get; set; }

        public QueryStatistics Baseline { get; set; }

        public QueryStatistics Current { get; set; }

        public double? AverageDurationChangePercent { get; set; }

        public double? P95DurationChangePercent { get; set; }

        public double? CpuChangePercent { get; set; }

        public double? LogicalReadsChangePercent { get; set; }

        public double? ExecutionCountChangePercent { get; set; }
    }

    public sealed class BaselineComparison
    {
        public BaselineComparison(IReadOnlyList<QueryComparison> items)
        {
            Items = items;
        }

        public IReadOnlyList<QueryComparison> Items { get; }
    }
}
