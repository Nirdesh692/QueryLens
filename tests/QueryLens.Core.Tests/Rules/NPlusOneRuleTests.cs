using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Rules;
using Xunit;

namespace QueryLens.Core.Tests.Rules
{
    public class NPlusOneRuleTests
    {
        private static QueryExecution Execution(string requestId)
        {
            return new QueryExecution
            {
                QueryHash = "HASH1",
                RequestId = requestId,
                Duration = TimeSpan.FromMilliseconds(5),
                StartTimeUtc = DateTime.UtcNow
            };
        }

        private static QueryStatistics Stats(long count)
        {
            return new QueryStatistics { QueryHash = "HASH1", ExecutionCount = count, AverageDurationMs = 5 };
        }

        private static RuleContext ContextWithRepeats(int count, QueryLensOptions options = null)
        {
            options = options ?? new QueryLensOptions();
            var executions = Enumerable.Range(0, count).Select(_ => Execution("req-1")).ToList();
            var requestGroups = new List<RequestQueryGroup> { new RequestQueryGroup("req-1", executions) };
            return new RuleContext(Stats(count), options, requestGroups);
        }

        [Fact]
        public void RepeatedQueryWithinOneRequest_IsFlaggedAsPossibleNPlusOne()
        {
            var findings = new NPlusOneRule().Evaluate(ContextWithRepeats(25)).ToList();

            Assert.Single(findings);
            Assert.Equal("NPLUSONE001", findings[0].RuleId);
            Assert.True(findings[0].Confidence > 0.9);
        }

        [Fact]
        public void FewRepeatsWithinOneRequest_IsNotFlagged()
        {
            Assert.Empty(new NPlusOneRule().Evaluate(ContextWithRepeats(2)));
        }

        [Fact]
        public void NPlusOneDisabledInOptions_ProducesNoFindings()
        {
            var options = new QueryLensOptions();
            options.Analysis.NPlusOne = false;

            Assert.Empty(new NPlusOneRule().Evaluate(ContextWithRepeats(50, options)));
        }

        [Fact]
        public void HighRepeatCount_YieldsCriticalSeverity()
        {
            var findings = new NPlusOneRule().Evaluate(ContextWithRepeats(100)).ToList();

            Assert.Equal(Severity.Critical, findings[0].Severity);
        }

        [Fact]
        public void ConfidenceIncreasesWithRepeatCount()
        {
            var lowCount = new NPlusOneRule().Evaluate(ContextWithRepeats(3)).Single().Confidence;
            var highCount = new NPlusOneRule().Evaluate(ContextWithRepeats(50)).Single().Confidence;

            Assert.True(highCount > lowCount);
        }

        [Fact]
        public void NoRequestGroups_ProducesNoFindings()
        {
            var options = new QueryLensOptions();
            var context = new RuleContext(Stats(0), options, new List<RequestQueryGroup>());

            Assert.Empty(new NPlusOneRule().Evaluate(context));
        }
    }
}
