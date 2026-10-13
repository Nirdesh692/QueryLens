using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Rules;
using Xunit;

namespace QueryLens.Core.Tests.Rules
{
    public class SlowQueryRuleTests
    {
        private static RuleContext ContextWith(double averageDurationMs)
        {
            var options = new QueryLensOptions();
            var stats = new QueryStatistics { QueryHash = "HASH1", AverageDurationMs = averageDurationMs };
            return new RuleContext(stats, options, new List<RequestQueryGroup>());
        }

        [Fact]
        public void DurationAboveSlowThreshold_ButBelowCritical_ProducesWarning()
        {
            var findings = new SlowQueryRule().Evaluate(ContextWith(600)).ToList();

            Assert.Single(findings);
            Assert.Equal(Severity.Warning, findings[0].Severity);
            Assert.Equal("SLOWQUERY001", findings[0].RuleId);
        }

        [Fact]
        public void DurationAtOrAboveCriticalThreshold_ProducesNoFinding()
        {
            Assert.Empty(new SlowQueryRule().Evaluate(ContextWith(1200)));
        }

        [Fact]
        public void DurationBelowThreshold_ProducesNoFinding()
        {
            Assert.Empty(new SlowQueryRule().Evaluate(ContextWith(50)));
        }
    }

    public class CriticalQueryRuleTests
    {
        private static RuleContext ContextWith(double averageDurationMs)
        {
            var options = new QueryLensOptions();
            var stats = new QueryStatistics { QueryHash = "HASH1", AverageDurationMs = averageDurationMs };
            return new RuleContext(stats, options, new List<RequestQueryGroup>());
        }

        [Fact]
        public void DurationAtOrAboveCriticalThreshold_ProducesCriticalFinding()
        {
            var findings = new CriticalQueryRule().Evaluate(ContextWith(1200)).ToList();

            Assert.Single(findings);
            Assert.Equal(Severity.Critical, findings[0].Severity);
            Assert.Equal("CRITICALQUERY001", findings[0].RuleId);
        }

        [Fact]
        public void DurationBelowCriticalThreshold_ProducesNoFinding()
        {
            Assert.Empty(new CriticalQueryRule().Evaluate(ContextWith(600)));
        }
    }
}
