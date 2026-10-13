using System;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Rules;
using Xunit;

namespace QueryLens.Core.Tests.Rules
{
    public class QueryExplosionRuleTests
    {
        private static QueryExecution Execution(string hash)
        {
            return new QueryExecution
            {
                QueryHash = hash,
                RequestId = "req-1",
                Duration = TimeSpan.FromMilliseconds(5),
                StartTimeUtc = DateTime.UtcNow
            };
        }

        private static RequestRuleContext ContextWithQueryCount(int count)
        {
            var options = new QueryLensOptions();
            var executions = Enumerable.Range(0, count).Select(i => Execution($"HASH{i}")).ToList();
            return new RequestRuleContext("req-1", executions, options);
        }

        [Fact]
        public void BelowWarningThreshold_ProducesNoFinding()
        {
            Assert.Empty(new QueryExplosionRule().Evaluate(ContextWithQueryCount(5)));
        }

        [Fact]
        public void AboveWarningThreshold_ProducesWarning()
        {
            var findings = new QueryExplosionRule().Evaluate(ContextWithQueryCount(25)).ToList();

            Assert.Single(findings);
            Assert.Equal(Severity.Warning, findings[0].Severity);
        }

        [Fact]
        public void AboveCriticalThreshold_ProducesCritical()
        {
            var findings = new QueryExplosionRule().Evaluate(ContextWithQueryCount(60)).ToList();

            Assert.Single(findings);
            Assert.Equal(Severity.Critical, findings[0].Severity);
        }
    }
}
