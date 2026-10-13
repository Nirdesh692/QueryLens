using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Everything a per-query rule needs: this query's aggregated statistics,
    /// the active configuration, and - for rules like N+1 detection that need
    /// to reason about a single request - this query's executions grouped by
    /// the request they occurred in.
    /// </summary>
    public sealed class RuleContext
    {
        public RuleContext(
            QueryStatistics statistics,
            QueryLensOptions options,
            IReadOnlyList<RequestQueryGroup> requestGroups)
        {
            Statistics = statistics;
            Options = options;
            RequestGroups = requestGroups ?? new List<RequestQueryGroup>();
        }

        public QueryStatistics Statistics { get; }

        public QueryLensOptions Options { get; }

        public IReadOnlyList<RequestQueryGroup> RequestGroups { get; }
    }

    /// <summary>This query's executions within one request/operation.</summary>
    public sealed class RequestQueryGroup
    {
        public RequestQueryGroup(string requestId, IReadOnlyList<QueryExecution> executions)
        {
            RequestId = requestId;
            Executions = executions;
        }

        public string RequestId { get; }

        public IReadOnlyList<QueryExecution> Executions { get; }
    }
}
