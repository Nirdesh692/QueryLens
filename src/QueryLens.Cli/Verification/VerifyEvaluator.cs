using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QueryLens.Core.Baselines;
using QueryLens.Core.Models;

namespace QueryLens.Cli.Verification
{
    public sealed class VerifyResult
    {
        public VerifyResult(IReadOnlyList<string> violations)
        {
            Violations = violations;
        }

        public IReadOnlyList<string> Violations { get; }

        public bool Passed => Violations.Count == 0;
    }

    /// <summary>The pass/fail logic behind <c>verify</c>; pure, no I/O.</summary>
    public static class VerifyEvaluator
    {
        /// <param name="maxDurationRegressionPercent">Max allowed growth of a query's average duration (percent), or null to skip.</param>
        /// <param name="maxQueryCount">Max allowed total query executions, or null to skip.</param>
        /// <param name="minBaselineMs">Queries whose baseline average is below this are ignored as noise.</param>
        public static VerifyResult Evaluate(
            IReadOnlyCollection<QueryStatistics> current,
            BaselineComparison comparison,
            double? maxDurationRegressionPercent,
            long? maxQueryCount,
            double minBaselineMs)
        {
            var violations = new List<string>();

            if (maxDurationRegressionPercent.HasValue && comparison != null)
            {
                foreach (var item in comparison.Items
                    .Where(i => i.Status == QueryComparisonStatus.Compared)
                    .OrderBy(i => i.QueryHash, System.StringComparer.Ordinal))
                {
                    if (item.Baseline.AverageDurationMs < minBaselineMs)
                    {
                        continue;
                    }

                    var change = item.AverageDurationChangePercent;
                    if (change.HasValue && change.Value > maxDurationRegressionPercent.Value)
                    {
                        violations.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "Query {0}: average duration regressed {1:0.0}% (limit {2:0.0}%).",
                            item.QueryHash, change.Value, maxDurationRegressionPercent.Value));
                    }
                }
            }

            if (maxQueryCount.HasValue)
            {
                var total = current.Sum(s => s.ExecutionCount);
                if (total > maxQueryCount.Value)
                {
                    violations.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Total query executions {0} exceeds limit {1}.", total, maxQueryCount.Value));
                }
            }

            return new VerifyResult(violations);
        }
    }
}
