using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Flags Hash Match operators processing a large number of rows.</summary>
    public sealed class ExpensiveHashMatchRule : IExecutionPlanRule
    {
        public string RuleId => "EXPENSIVEHASH001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (op.PhysicalOp != "Hash Match" || op.EffectiveEstimatedRows < options.Thresholds.ExpensiveHashRowThreshold)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "High-cost Hash Match",
                    Description = $"Hash Match ({op.LogicalOp}) estimated to process {op.EffectiveEstimatedRows:N0} rows.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "Hash Match is often fine for large unsorted joins/aggregates, but an index enabling a Merge Join or narrower input could reduce cost - worth reviewing, not necessarily wrong.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["logicalOp"] = op.LogicalOp ?? "",
                        ["effectiveEstimatedRows"] = op.EffectiveEstimatedRows.ToString("N0")
                    }
                };
            }
        }
    }
}
