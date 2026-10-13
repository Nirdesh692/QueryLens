using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Flags requests/operations that issue an unusually large number of
    /// database queries, regardless of whether any single query is slow.
    /// </summary>
    public sealed class QueryExplosionRule : IRequestAnalysisRule
    {
        public string RuleId => "QUERYEXPLOSION001";

        public IEnumerable<Finding> Evaluate(RequestRuleContext context)
        {
            var thresholds = context.Options.Thresholds;
            var count = context.Executions.Count;

            if (count < thresholds.MaxQueriesPerRequestWarning)
            {
                yield break;
            }

            var severity = count >= thresholds.MaxQueriesPerRequestCritical
                ? Severity.Critical
                : Severity.Warning;

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = severity,
                Title = "Query explosion",
                Description = $"Request '{context.RequestId}' issued {count} database queries.",
                QueryHash = null,
                Confidence = 1.0,
                Recommendation = "Review the request for opportunities to batch, cache, or eager-load related data.",
                Evidence = new Dictionary<string, string>
                {
                    ["requestId"] = context.RequestId,
                    ["queryCount"] = count.ToString(),
                    ["distinctQueries"] = context.Executions.Select(e => e.QueryHash).Distinct().Count().ToString()
                }
            };
        }
    }
}
