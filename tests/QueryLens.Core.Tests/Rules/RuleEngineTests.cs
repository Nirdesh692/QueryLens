using System;
using System.Linq;
using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Rules;
using Xunit;

namespace QueryLens.Core.Tests.Rules
{
    public class RuleEngineTests
    {
        [Fact]
        public void Evaluate_DetectsNPlusOneAndExplosionAcrossRawExecutions()
        {
            var engine = RuleEngine.CreateDefault();
            var options = new QueryLensOptions();

            var executions = Enumerable.Range(1, 30)
                .Select(i => new QueryExecution
                {
                    QueryHash = "HASH1",
                    RequestId = "req-1",
                    Duration = TimeSpan.FromMilliseconds(5),
                    StartTimeUtc = DateTime.UtcNow
                })
                .ToList();

            var statistics = new List<QueryStatistics>
            {
                new QueryStatistics { QueryHash = "HASH1", ExecutionCount = 30, AverageDurationMs = 5 }
            };

            var findings = engine.Evaluate(statistics, executions, options);

            Assert.Contains(findings, f => f.RuleId == "NPLUSONE001");
            Assert.Contains(findings, f => f.RuleId == "QUERYEXPLOSION001");
        }

        [Fact]
        public void Evaluate_WithNoIssues_ProducesNoFindings()
        {
            var engine = RuleEngine.CreateDefault();
            var options = new QueryLensOptions();

            var executions = new List<QueryExecution>
            {
                new QueryExecution { QueryHash = "HASH1", RequestId = "req-1", Duration = TimeSpan.FromMilliseconds(5), StartTimeUtc = DateTime.UtcNow }
            };

            var statistics = new List<QueryStatistics>
            {
                new QueryStatistics { QueryHash = "HASH1", ExecutionCount = 1, AverageDurationMs = 5 }
            };

            var findings = engine.Evaluate(statistics, executions, options);

            Assert.Empty(findings);
        }

        [Fact]
        public void Evaluate_DetectsCriticalDuration()
        {
            var engine = RuleEngine.CreateDefault();
            var options = new QueryLensOptions();

            var statistics = new List<QueryStatistics>
            {
                new QueryStatistics { QueryHash = "HASH1", ExecutionCount = 1, AverageDurationMs = 1500 }
            };

            var findings = engine.Evaluate(statistics, new List<QueryExecution>(), options);

            Assert.Contains(findings, f => f.RuleId == "CRITICALQUERY001" && f.Severity == Severity.Critical);
        }
    }
}
