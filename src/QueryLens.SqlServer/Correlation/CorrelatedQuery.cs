using QueryLens.Core.Models;
using QueryLens.SqlServer.ExecutionPlans;
using QueryLens.SqlServer.QueryStore;

namespace QueryLens.SqlServer.Correlation
{
    /// <summary>
    /// One logical query, with whichever side(s) saw it. Either
    /// <see cref="ApplicationStatistics"/> or <see cref="QueryStoreEntry"/> can be
    /// null on its own - a query only SQL Server retained (application capture
    /// wasn't running, or it predates the app starting) has no app-side stats;
    /// a query the app captured but Query Store has no matching entry for
    /// (retention window expired, or it's a query shape QS doesn't track) has
    /// no Query Store entry.
    /// </summary>
    public sealed class CorrelatedQuery
    {
        /// <summary>QueryLens's own fingerprint hash - the correlation key.</summary>
        public string QueryHash { get; set; }

        public QueryStatistics ApplicationStatistics { get; set; }

        public QueryStoreEntry QueryStoreEntry { get; set; }

        /// <summary>
        /// The parsed operator tree from QueryStoreEntry.ExecutionPlanXml, if
        /// present and parseable. Populated by QueryLensSqlServer.AnalyzeWithQueryStoreAsync;
        /// null if there was no plan XML to parse (or execution-plan analysis is disabled).
        /// </summary>
        public ExecutionPlan ParsedExecutionPlan { get; set; }

        public bool HasApplicationData => ApplicationStatistics != null;

        public bool HasQueryStoreData => QueryStoreEntry != null;
    }
}
