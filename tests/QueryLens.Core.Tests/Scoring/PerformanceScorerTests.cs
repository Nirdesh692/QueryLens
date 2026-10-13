using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;
using QueryLens.Core.Scoring;
using Xunit;

namespace QueryLens.Core.Tests.Scoring
{
    public class PerformanceScorerTests
    {
        private readonly PerformanceScorer _scorer = new PerformanceScorer();

        [Fact]
        public void NoFindings_ProducesPerfectScore()
        {
            var result = _scorer.Score(new List<QueryStatistics>(), new List<Finding>());

            Assert.Equal(100, result.Score);
            Assert.All(result.Components, c => Assert.Equal(0, c.Penalty));
        }

        [Fact]
        public void CriticalFinding_ReducesScoreByItsWeight()
        {
            var findings = new List<Finding>
            {
                new Finding { RuleId = "CRITICALQUERY001", Severity = Severity.Critical, Confidence = 1.0 }
            };

            var result = _scorer.Score(new List<QueryStatistics>(), findings);

            Assert.Equal(90, result.Score);
        }

        [Fact]
        public void PenaltyPerCategory_IsCapped()
        {
            var findings = new List<Finding>();
            for (var i = 0; i < 50; i++)
            {
                findings.Add(new Finding { RuleId = "CRITICALQUERY001", Severity = Severity.Critical, Confidence = 1.0 });
            }

            var result = _scorer.Score(new List<QueryStatistics>(), findings);

            // 50 * weight(10) = 500 raw, capped at 25 for the Duration category.
            Assert.Equal(75, result.Score);
        }

        [Fact]
        public void ScoreNeverGoesBelowZero()
        {
            var ruleIds = new[]
            {
                "CRITICALQUERY001", "HIGHREADS001", "HIGHCPU001",
                "QUERYEXPLOSION001", "NPLUSONE001", "LARGERESULT001", "QUERYREGRESSION001"
            };

            var findings = new List<Finding>();
            foreach (var ruleId in ruleIds)
            {
                for (var i = 0; i < 5; i++)
                {
                    findings.Add(new Finding { RuleId = ruleId, Severity = Severity.Critical, Confidence = 1.0 });
                }
            }

            var result = _scorer.Score(new List<QueryStatistics>(), findings);

            Assert.Equal(0, result.Score);
        }

        [Fact]
        public void ComponentsAreReturnedForEveryCategory()
        {
            var result = _scorer.Score(new List<QueryStatistics>(), new List<Finding>());

            Assert.Contains(result.Components, c => c.Name == "Duration");
            Assert.Contains(result.Components, c => c.Name == "N+1");
            Assert.Contains(result.Components, c => c.Name == "Query Count");
            Assert.Contains(result.Components, c => c.Name == "Regression");
        }

        [Fact]
        public void LowerConfidenceFinding_ResultsInHigherScoreThanHigherConfidence()
        {
            var lowConfidence = new List<Finding>
            {
                new Finding { RuleId = "NPLUSONE001", Severity = Severity.Warning, Confidence = 0.6 }
            };
            var highConfidence = new List<Finding>
            {
                new Finding { RuleId = "NPLUSONE001", Severity = Severity.Warning, Confidence = 0.99 }
            };

            var lowResult = _scorer.Score(new List<QueryStatistics>(), lowConfidence);
            var highResult = _scorer.Score(new List<QueryStatistics>(), highConfidence);

            Assert.True(highResult.Score < lowResult.Score);
        }

        [Fact]
        public void UnrecognizedRuleId_FallsIntoOtherCategory()
        {
            var findings = new List<Finding>
            {
                new Finding { RuleId = "SOMETHINGNEW999", Severity = Severity.Warning, Confidence = 1.0 }
            };

            var result = _scorer.Score(new List<QueryStatistics>(), findings);

            var otherComponent = result.Components.Single(c => c.Name == "Other");
            Assert.NotEqual(0, otherComponent.Penalty);
        }

        [Fact]
        public void ExecutionPlanRuleIds_MapToLogicalReadsOrCpuCategories()
        {
            var findings = new List<Finding>
            {
                new Finding { RuleId = "LARGESCAN001", Severity = Severity.Warning, Confidence = 1.0 },
                new Finding { RuleId = "KEYLOOKUP001", Severity = Severity.Warning, Confidence = 1.0 },
                new Finding { RuleId = "MISSINGINDEX001", Severity = Severity.Warning, Confidence = 1.0 },
                new Finding { RuleId = "EXPENSIVESORT001", Severity = Severity.Warning, Confidence = 1.0 },
                new Finding { RuleId = "EXPENSIVEHASH001", Severity = Severity.Warning, Confidence = 1.0 },
                new Finding { RuleId = "NESTEDLOOPSLARGE001", Severity = Severity.Warning, Confidence = 1.0 },
                new Finding { RuleId = "SPOOL001", Severity = Severity.Warning, Confidence = 1.0 }
            };

            var result = _scorer.Score(new List<QueryStatistics>(), findings);

            var readsComponent = result.Components.Single(c => c.Name == "Logical Reads");
            var cpuComponent = result.Components.Single(c => c.Name == "CPU");

            Assert.NotEqual(0, readsComponent.Penalty);
            Assert.NotEqual(0, cpuComponent.Penalty);
        }
    }
}
