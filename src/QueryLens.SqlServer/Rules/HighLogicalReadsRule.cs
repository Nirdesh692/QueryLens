using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.SqlServer.Correlation;

namespace QueryLens.SqlServer.Rules
{
    /// <summary>Flags queries with high average logical reads (uses the "Logical Reads" score category).</summary>
    public sealed class HighLogicalReadsRule : IQueryStoreAnalysisRule
    {
        public string RuleId => "HIGHREADS001";

        public IEnumerable<Finding> Evaluate(CorrelatedQuery query, QueryLensOptions options)
        {
            var entry = query?.QueryStoreEntry;
            if (entry == null)
            {
                yield break;
            }

            if (entry.AvgLogicalReads >= options.Thresholds.HighLogicalReads)
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "High logical reads",
                    Description = $"Query Store reports {entry.AvgLogicalReads:N0} average logical reads, at or above the {options.Thresholds.HighLogicalReads:N0} threshold.",
                    QueryHash = query.QueryHash,
                    Confidence = 1.0,
                    Recommendation = "Investigate whether a missing or non-covering index is forcing a larger scan than necessary.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["avgLogicalReads"] = entry.AvgLogicalReads.ToString("N0"),
                        ["executionCount"] = entry.ExecutionCount.ToString()
                    }
                };
            }
        }
    }
}
