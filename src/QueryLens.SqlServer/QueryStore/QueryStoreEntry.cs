using System;

namespace QueryLens.SqlServer.QueryStore
{
    /// <summary>
    /// One query's aggregated Query Store data, as retrieved from SQL Server's
    /// sys.query_store_* catalog views. Time-based fields are already converted
    /// to milliseconds (Query Store itself reports them in microseconds).
    /// </summary>
    public sealed class QueryStoreEntry
    {
        public long QueryId { get; set; }

        public long PlanId { get; set; }

        /// <summary>The SQL text Query Store captured - fed through QueryLens's own fingerprinter for correlation.</summary>
        public string QueryText { get; set; }

        public long ExecutionCount { get; set; }

        public double AvgDurationMs { get; set; }

        public double AvgCpuMs { get; set; }

        public double AvgLogicalReads { get; set; }

        public double AvgPhysicalReads { get; set; }

        /// <summary>Average query memory grant, in KB. Null if not available (e.g. older SQL Server version).</summary>
        public double? AvgMemoryGrantKb { get; set; }

        public DateTime? LastExecutionTimeUtc { get; set; }

        /// <summary>Raw execution plan XML, unparsed - plan analysis is a later phase.</summary>
        public string ExecutionPlanXml { get; set; }
    }
}
