using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Flags queries that return unusually large result sets - suspicious
    /// regardless of how quickly the query itself executes.
    /// </summary>
    public sealed class LargeResultSetRule : IQueryAnalysisRule
    {
        public string RuleId => "LARGERESULT001";

        public IEnumerable<Finding> Evaluate(RuleContext context)
        {
            var stats = context.Statistics;
            var thresholds = context.Options.Thresholds;

            if (!stats.MaxRowsReturned.HasValue)
            {
                yield break;
            }

            if (stats.MaxRowsReturned.Value >= thresholds.MaxRowsReturned)
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "Large result set",
                    Description = $"Query returned up to {stats.MaxRowsReturned.Value:N0} rows, at or above the {thresholds.MaxRowsReturned:N0}-row threshold.",
                    QueryHash = stats.QueryHash,
                    Confidence = 1.0,
                    Recommendation = "Check for missing pagination, excessive Include()/eager-loading, or a missing projection.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["maxRowsReturned"] = stats.MaxRowsReturned.Value.ToString("N0")
                    }
                };
            }
        }
    }
}
