using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.Core.Baselines
{
    /// <summary>
    /// Turns a <see cref="BaselineComparison"/> into QUERYREGRESSION001 findings:
    /// one per query whose average duration, CPU or logical reads grew beyond
    /// the configured tolerance. Not an <c>IQueryAnalysisRule</c> because it
    /// needs a baseline, which per-query rules do not receive.
    /// </summary>
    public sealed class QueryRegressionRule
    {
        public const string RuleId = "QUERYREGRESSION001";

        public IReadOnlyList<Finding> Evaluate(BaselineComparison comparison, QueryLensOptions options)
        {
            var findings = new List<Finding>();

            if (comparison == null || options == null || !options.Analysis.QueryRegression)
            {
                return findings;
            }

            var t = options.Thresholds;

            foreach (var item in comparison.Items.Where(i => i.Status == QueryComparisonStatus.Compared))
            {
                var regressed = new List<KeyValuePair<string, double>>();

                if (item.Baseline.AverageDurationMs >= t.RegressionMinBaselineMs)
                {
                    Add(regressed, "averageDuration", item.AverageDurationChangePercent, t);
                }

                if (item.Baseline.AverageCpuMs.GetValueOrDefault() >= t.RegressionMinBaselineMs)
                {
                    Add(regressed, "cpu", item.CpuChangePercent, t);
                }

                Add(regressed, "logicalReads", item.LogicalReadsChangePercent, t);

                if (regressed.Count == 0)
                {
                    continue;
                }

                var worst = regressed.Max(r => r.Value);
                var evidence = new Dictionary<string, string>
                {
                    ["baselineAverageDurationMs"] = Format(item.Baseline.AverageDurationMs),
                    ["currentAverageDurationMs"] = Format(item.Current.AverageDurationMs)
                };

                foreach (var r in regressed)
                {
                    evidence[r.Key + "ChangePercent"] = Format(r.Value);
                }

                findings.Add(new Finding
                {
                    RuleId = RuleId,
                    Severity = worst >= t.RegressionCriticalPercent ? Severity.Critical : Severity.Warning,
                    Title = "Query performance regression",
                    Description = "Regressed versus baseline: " +
                        string.Join(", ", regressed.Select(r => $"{r.Key} +{r.Value:F0}%")) +
                        $" (tolerance {t.RegressionTolerancePercent:F0}%).",
                    QueryHash = item.QueryHash,
                    Confidence = 1.0,
                    Recommendation = "Compare the execution plan with the baseline release; look for a changed plan, dropped index, or new data volume.",
                    Evidence = evidence
                });
            }

            return findings;
        }

        private static void Add(List<KeyValuePair<string, double>> list, string name, double? change, ThresholdOptions t)
        {
            if (change.HasValue && change.Value >= t.RegressionTolerancePercent)
            {
                list.Add(new KeyValuePair<string, double>(name, change.Value));
            }
        }

        private static string Format(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
