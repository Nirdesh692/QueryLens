using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Rules;
using Xunit;

namespace QueryLens.Core.Tests.Rules
{
    public class LargeResultSetRuleTests
    {
        private static RuleContext ContextWith(long? maxRowsReturned)
        {
            var options = new QueryLensOptions();
            var stats = new QueryStatistics { QueryHash = "HASH1", MaxRowsReturned = maxRowsReturned };
            return new RuleContext(stats, options, new List<RequestQueryGroup>());
        }

        [Fact]
        public void RowsAtOrAboveThreshold_ProducesWarning()
        {
            var findings = new LargeResultSetRule().Evaluate(ContextWith(250000)).ToList();

            Assert.Single(findings);
            Assert.Equal(Severity.Warning, findings[0].Severity);
        }

        [Fact]
        public void RowsBelowThreshold_ProducesNoFinding()
        {
            Assert.Empty(new LargeResultSetRule().Evaluate(ContextWith(100)));
        }

        [Fact]
        public void NoRowsDataAvailable_ProducesNoFinding()
        {
            Assert.Empty(new LargeResultSetRule().Evaluate(ContextWith(null)));
        }
    }
}
