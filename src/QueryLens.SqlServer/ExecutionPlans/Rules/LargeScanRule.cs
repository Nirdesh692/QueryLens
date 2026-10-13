using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>
    /// Flags Table Scan / Clustered Index Scan / Index Scan operators processing
    /// a large number of rows - the classic "possible missing index" signal.
    /// Deliberately advisory: this names the table and predicate-bearing object,
    /// never claims an index is definitely missing (see MissingIndexRule for
    /// SQL Server's own, more concrete suggestion).
    /// </summary>
    public sealed class LargeScanRule : IExecutionPlanRule
    {
        private static readonly HashSet<string> ScanOps = new HashSet<string>
        {
            "Table Scan", "Clustered Index Scan", "Index Scan"
        };

        public string RuleId => "LARGESCAN001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var op in plan?.AllOperators ?? Enumerable.Empty<PlanOperator>())
            {
                if (!ScanOps.Contains(op.PhysicalOp ?? string.Empty))
                {
                    continue;
                }

                if (op.EffectiveEstimatedRows < options.Thresholds.LargeScanRowThreshold)
                {
                    continue;
                }

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = $"Large {op.PhysicalOp}",
                    Description = $"{op.PhysicalOp} on {op.ObjectName ?? "an unnamed object"} is estimated to process {op.EffectiveEstimatedRows:N0} rows.",
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "Consider whether an index on the columns this query filters or joins on would let SQL Server seek instead of scan.",
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
