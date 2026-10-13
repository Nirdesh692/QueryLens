using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Flags Spool operators (Table/Index/Row Count/Window Spool) processing a large number of rows.</summary>
    public sealed class SpoolRule : IExecutionPlanRule
    {
        public string RuleId => "SPOOL001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (op.PhysicalOp == null || !op.PhysicalOp.Contains("Spool"))
                {
                    continue;
                }

                if (op.EffectiveEstimatedRows < options.Thresholds.SpoolRowThreshold)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = $"Large {op.PhysicalOp}",
                    Description = $"{op.PhysicalOp} estimated to materialize {op.EffectiveEstimatedRows:N0} rows.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "Spools are normal for some plan shapes (e.g. recursive CTEs) but can also indicate the optimizer working around a missing index - review whether it's expected here.",
                    Evidence = new Dictionary<string, string> { ["effectiveEstimatedRows"] = op.EffectiveEstimatedRows.ToString("N0") }
                };
            }
        }
    }
}
