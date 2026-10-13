using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Surfaces SQL Server's own missing-index suggestion, if the plan included one.</summary>
    public sealed class MissingIndexRule : IExecutionPlanRule
    {
        public string RuleId => "MISSINGINDEX001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            foreach (var suggestion in plan?.MissingIndexSuggestions ?? Enumerable.Empty<MissingIndexSuggestion>())
            {
                var equality = string.Join(", ", suggestion.EqualityColumns);
                var inequality = string.Join(", ", suggestion.InequalityColumns);
                var include = string.Join(", ", suggestion.IncludedColumns);

                var keyColumns = string.Join(", ", new[] { equality, inequality }.Where(s => !string.IsNullOrEmpty(s)));

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = suggestion.Impact >= 50 ? Severity.Warning : Severity.Info,
                    Title = "SQL Server suggests a missing index",
                    Description = $"SQL Server estimates ~{suggestion.Impact:F0}% improvement from an index on {suggestion.Table} ({keyColumns})" +
                                  (string.IsNullOrEmpty(include) ? "." : $" including ({include})."),
                    QueryHash = queryHash,
                    Confidence = 1.0,
                    Recommendation = "This is SQL Server's own suggestion, not QueryLens's - review it like any missing-index hint: verify against actual workload before creating it, and check for near-duplicate existing indexes.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["table"] = suggestion.Table ?? "",
                        ["impact"] = suggestion.Impact.ToString("F1"),
                        ["equalityColumns"] = equality,
                        ["inequalityColumns"] = inequality,
                        ["includedColumns"] = include
                    }
                };
            }
        }
    }
}
