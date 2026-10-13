using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Flags queries whose average duration has crossed the configured
    /// "critical" threshold.
    /// </summary>
    public sealed class CriticalQueryRule : IQueryAnalysisRule
    {
        public string RuleId => "CRITICALQUERY001";

        public IEnumerable<Finding> Evaluate(RuleContext context)
        {
            var stats = context.Statistics;
            var thresholds = context.Options.Thresholds;

            if (stats.AverageDurationMs >= thresholds.CriticalQueryMs)
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Critical,
                    Title = "Critical query duration",
                    Description = $"Average duration {stats.AverageDurationMs:F0}ms is at or above the {thresholds.CriticalQueryMs:F0}ms critical threshold.",
                    QueryHash = stats.QueryHash,
                    Confidence = 1.0,
                    Recommendation = "Investigate the execution plan and consider indexing, query rewrites, or caching.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["averageDurationMs"] = stats.AverageDurationMs.ToString("F1"),
                        ["p99DurationMs"] = stats.P99DurationMs.ToString("F1"),
                        ["executionCount"] = stats.ExecutionCount.ToString()
                    }
                };
            }
        }
    }
}
