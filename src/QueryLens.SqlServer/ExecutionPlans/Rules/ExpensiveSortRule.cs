using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Flags Sort operators processing a large number of rows.</summary>
    public sealed class ExpensiveSortRule : IExecutionPlanRule
    {
        public string RuleId => "EXPENSIVESORT001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (op.PhysicalOp != "Sort" || op.EffectiveEstimatedRows < options.Thresholds.ExpensiveSortRowThreshold)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "High-cost Sort",
                    Description = $"Sort operator estimated to process {op.EffectiveEstimatedRows:N0} rows.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "An index matching the ORDER BY (or the join/grouping that requires sorted input) could let SQL Server avoid sorting at runtime.",
                    Evidence = new Dictionary<string, string> { ["effectiveEstimatedRows"] = op.EffectiveEstimatedRows.ToString("N0") }
                };
            }
        }
    }
}
