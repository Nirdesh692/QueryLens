using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>
    /// Flags a large gap between estimated and actual row counts - a sign of
    /// stale statistics or a cardinality-estimation edge case. Only fires when
    /// the plan carries actual runtime data (not the common Query-Store-estimated case).
    /// </summary>
    public sealed class RowCountDiscrepancyRule : IExecutionPlanRule
    {
        public string RuleId => "ROWCOUNTDISCREPANCY001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (!op.ActualRows.HasValue || op.EstimateRows <= 0 || op.ActualRows.Value <= 0)
                {
                    continue;
                }

                var bigger = Math.Max(op.EstimateRows, op.ActualRows.Value);
                var smaller = Math.Min(op.EstimateRows, op.ActualRows.Value);
                var ratio = bigger / smaller;

                if (ratio < options.Thresholds.RowCountDiscrepancyRatio)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = $"{op.PhysicalOp} estimate is {ratio:F0}x off from actual",
                    Description = $"Estimated {op.EstimateRows:N0} rows but actual was {op.ActualRows.Value:N0}.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "Consider updating statistics on the underlying table(s), or check for a parameter-sniffing scenario if this varies by input.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["estimateRows"] = op.EstimateRows.ToString("N0"),
                        ["actualRows"] = op.ActualRows.Value.ToString("N0"),
                        ["ratio"] = ratio.ToString("F1")
                    }
                };
            }
        }
    }
}
