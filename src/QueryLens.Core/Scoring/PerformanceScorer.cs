using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;

namespace QueryLens.Core.Scoring
{
    /// <summary>
    /// Produces a 0-100 performance score with a fully transparent, per-category
    /// breakdown - every point deducted is traceable to a specific finding
    /// category, never a black-box number. Each finding's penalty is scaled by
    /// its confidence, so a low-confidence N+1 guess costs less than a
    /// deterministic critical-duration finding.
    /// </summary>
    public sealed class PerformanceScorer : IPerformanceScorer
    {
        private const double StartingScore = 100;

        // Caps how much any single category can drag the score down, so one
        // noisy category (e.g. hundreds of N+1 findings) doesn't dominate.
        private const double MaxPenaltyPerCategory = 25;

        private const double CriticalWeight = 10;
        private const double WarningWeight = 4;
        private const double InfoWeight = 1;

        private static readonly IReadOnlyDictionary<string, string> RuleCategories =
            new Dictionary<string, string>
            {
                ["SLOWQUERY001"] = "Duration",
                ["CRITICALQUERY001"] = "Duration",
                ["LARGERESULT001"] = "Result Size",
                ["NPLUSONE001"] = "N+1",
                ["QUERYEXPLOSION001"] = "Query Count",
                // Reserved for later phases (execution-plan analysis, Query
                // Store integration, CI/CD baselines) so the score schema
                // does not need to change shape when those rules land.
                ["QUERYREGRESSION001"] = "Regression",
                ["HIGHCPU001"] = "CPU",
                ["HIGHREADS001"] = "Logical Reads",
                // Execution-plan rules (Phase 6): scans/lookups/missing indexes
                // are fundamentally about reads; sorts/hashes/spools/large
                // nested loops are compute-bound. RowCountDiscrepancy and
                // Parallelism are informational and fall through to "Other".
                ["LARGESCAN001"] = "Logical Reads",
                ["KEYLOOKUP001"] = "Logical Reads",
                ["MISSINGINDEX001"] = "Logical Reads",
                ["EXPENSIVESORT001"] = "CPU",
                ["EXPENSIVEHASH001"] = "CPU",
                ["NESTEDLOOPSLARGE001"] = "CPU",
                ["SPOOL001"] = "CPU"
            };

        private static readonly string[] CategoryOrder =
        {
            "Duration", "Logical Reads", "CPU", "Query Count", "N+1", "Result Size", "Regression", "Other"
        };

        public PerformanceScoreResult Score(
            IReadOnlyCollection<QueryStatistics> statistics,
            IReadOnlyList<Finding> findings)
        {
            findings = findings ?? new List<Finding>();

            var rawPenaltyByCategory = CategoryOrder.ToDictionary(c => c, c => 0.0);
            var countByCategory = CategoryOrder.ToDictionary(c => c, c => 0);

            foreach (var finding in findings)
            {
                var category = CategoryFor(finding.RuleId);
                var weight = WeightFor(finding.Severity);
                var penalty = weight * Clamp01(finding.Confidence);

                rawPenaltyByCategory[category] += penalty;
                countByCategory[category]++;
            }

            var components = new List<ScoreComponent>();
            double totalPenalty = 0;

            foreach (var category in CategoryOrder)
            {
                var cappedPenalty = Math.Min(rawPenaltyByCategory[category], MaxPenaltyPerCategory);
                totalPenalty += cappedPenalty;

                components.Add(new ScoreComponent
                {
                    Name = category,
                    Penalty = -Math.Round(cappedPenalty, 1),
                    Reason = countByCategory[category] > 0
                        ? $"{countByCategory[category]} finding(s) in this category"
                        : "No findings in this category"
                });
            }

            var finalScore = Clamp(StartingScore - totalPenalty, 0, 100);

            return new PerformanceScoreResult
            {
                Score = (int)Math.Round(finalScore),
                Components = components
            };
        }

        private static string CategoryFor(string ruleId)
        {
            if (ruleId != null && RuleCategories.TryGetValue(ruleId, out var category))
            {
                return category;
            }

            return "Other";
        }

        private static double WeightFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Critical:
                    return CriticalWeight;
                case Severity.Warning:
                    return WarningWeight;
                default:
                    return InfoWeight;
            }
        }

        private static double Clamp01(double value)
        {
            return double.IsNaN(value) ? 1.0 : Clamp(value, 0, 1);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
