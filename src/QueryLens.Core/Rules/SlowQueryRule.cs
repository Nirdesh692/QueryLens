using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Flags queries whose average duration exceeds the configured "slow"
    /// threshold but has not yet reached the "critical" threshold (see
    /// <see cref="CriticalQueryRule"/>, which takes over from there).
    /// </summary>
    public sealed class SlowQueryRule : IQueryAnalysisRule
    {
        public string RuleId => "SLOWQUERY001";

        public IEnumerable<Finding> Evaluate(RuleContext context)
        {
            var stats = context.Statistics;
            var thresholds = context.Options.Thresholds;

            if (stats.AverageDurationMs >= thresholds.SlowQueryMs &&
                stats.AverageDurationMs < thresholds.CriticalQueryMs)
            {
                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = Severity.Warning,
                    Title = "Slow query",
                    Description = $"Average duration {stats.AverageDurationMs:F0}ms exceeds the {thresholds.SlowQueryMs:F0}ms threshold.",
                    QueryHash = stats.QueryHash,
                    Confidence = 1.0,
                    Recommendation = "Review the query and its execution plan for missing indexes or excessive scanning.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["averageDurationMs"] = stats.AverageDurationMs.ToString("F1"),
                        ["p95DurationMs"] = stats.P95DurationMs.ToString("F1"),
                        ["executionCount"] = stats.ExecutionCount.ToString()
                    }
                };
            }
        }
    }
}
