namespace QueryLens.Core.Models
{
    /// <summary>
    /// Aggregated statistics for one logical query (identified by QueryHash)
    /// across all executions observed so far. Never just an average -
    /// percentiles are tracked because averages hide outliers.
    /// </summary>
    public sealed class QueryStatistics
    {
        public string QueryHash { get; set; }

        public string NormalizedSql { get; set; }

        public string RepresentativeSql { get; set; }

        public long ExecutionCount { get; set; }

        public double MinDurationMs { get; set; }

        public double MaxDurationMs { get; set; }

        public double AverageDurationMs { get; set; }

        public double TotalDurationMs { get; set; }

        public double P50DurationMs { get; set; }

        public double P90DurationMs { get; set; }

        public double P95DurationMs { get; set; }

        public double P99DurationMs { get; set; }

        public double? AverageCpuMs { get; set; }

        public double? AverageLogicalReads { get; set; }

        public double? AveragePhysicalReads { get; set; }

        public double? AverageRowsReturned { get; set; }

        public long? MaxRowsReturned { get; set; }
    }
}
