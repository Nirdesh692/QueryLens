using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Flags Key Lookup / RID Lookup operators processing a large number of rows - often a sign of a non-covering index.</summary>
    public sealed class KeyLookupRule : IExecutionPlanRule
    {
        private static readonly HashSet<string> LookupOps = new HashSet<string> { "Key Lookup", "RID Lookup" };

        public string RuleId => "KEYLOOKUP001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (!LookupOps.Contains(op.PhysicalOp ?? string.Empty))
                {
                    continue;
                }

                if (op.EffectiveEstimatedRows < options.Thresholds.KeyLookupRowThreshold)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = $"Frequent {op.PhysicalOp}",
                    Description = $"{op.PhysicalOp} on {op.ObjectName ?? "an unnamed object"} runs an estimated {op.EffectiveEstimatedRows:N0} times.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "Consider a covering index (adding the selected columns as INCLUDEs) to avoid the extra lookup per row.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["physicalOp"] = op.PhysicalOp,
                        ["object"] = op.ObjectName ?? "",
                        ["effectiveEstimatedRows"] = op.EffectiveEstimatedRows.ToString("N0")
                    }
                };
            }
        }
    }
}
