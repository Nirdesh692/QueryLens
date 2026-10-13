using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Flags a Nested Loops join whose outer (driving) input is large - each outer row re-executes the inner side.</summary>
    public sealed class NestedLoopsRule : IExecutionPlanRule
    {
        public string RuleId => "NESTEDLOOPSLARGE001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (op.PhysicalOp != "Nested Loops" || op.Children.Count == 0)
                {
                    continue;
                }

                // The first child is conventionally the outer (driving) input.
                var outer = op.Children[0];
                if (outer.EffectiveEstimatedRows < options.Thresholds.LargeScanRowThreshold)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "Nested Loops over a large outer input",
                    Description = $"Nested Loops join's outer input is estimated at {outer.EffectiveEstimatedRows:N0} rows - the inner side runs once per outer row.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "Nested Loops suits a small, selective outer input. If the outer side can't be narrowed, a Hash or Merge join may scale better.",
                    Evidence = new Dictionary<string, string> { ["outerEffectiveEstimatedRows"] = outer.EffectiveEstimatedRows.ToString("N0") }
                };
            }
        }
    }
}
