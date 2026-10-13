using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.SqlServer.Correlation;

namespace QueryLens.SqlServer.Rules
{
    /// <summary>
    /// Flags queries with high average CPU time, using the RuleId QueryLens's
    /// scorer already reserves a "CPU" score category for (see
    /// QueryLens.Core.Scoring.PerformanceScorer) - no scorer changes needed
    /// for this finding to show up correctly in the breakdown.
    /// </summary>
    public sealed class HighCpuRule : IQueryStoreAnalysisRule
    {
        public string RuleId => "HIGHCPU001";

        public IEnumerable<Finding> Evaluate(CorrelatedQuery query, QueryLensOptions options)
        {
            var entry = query?.QueryStoreEntry;
            if (entry == null)
            {
                yield break;
            }

            if (entry.AvgCpuMs >= options.Thresholds.HighCpuMs)
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "High CPU query",
                    Description = $"Query Store reports {entry.AvgCpuMs:F0}ms average CPU time, at or above the {options.Thresholds.HighCpuMs:F0}ms threshold.",
                    QueryHash = query.QueryHash,
                    Confidence = 1.0,
                    Recommendation = "Review the execution plan for expensive operators (sorts, hash joins, scalar UDFs) driving CPU cost.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["avgCpuMs"] = entry.AvgCpuMs.ToString("F1"),
                        ["executionCount"] = entry.ExecutionCount.ToString()
                    }
                };
            }
        }
    }
}
