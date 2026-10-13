using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Everything a request-level rule needs: every query execution (across
    /// all distinct queries) observed within one request/operation. Used by
    /// rules such as query-explosion detection that care about totals rather
    /// than any single query.
    /// </summary>
    public sealed class RequestRuleContext
    {
        public RequestRuleContext(string requestId, IReadOnlyList<QueryExecution> executions, QueryLensOptions options)
        {
            RequestId = requestId;
            Executions = executions;
            Options = options;
        }

        public string RequestId { get; }

        public IReadOnlyList<QueryExecution> Executions { get; }

        public QueryLensOptions Options { get; }
    }
}
