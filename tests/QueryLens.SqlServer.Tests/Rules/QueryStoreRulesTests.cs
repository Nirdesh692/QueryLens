using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.SqlServer.Correlation;
using QueryLens.SqlServer.QueryStore;
using QueryLens.SqlServer.Rules;
using Xunit;

namespace QueryLens.SqlServer.Tests.Rules
{
    public class HighCpuRuleTests
    {
        [Fact]
        public void AboveThreshold_ProducesFinding()
        {
            var options = new QueryLensOptions();
            var query = new CorrelatedQuery
            {
                QueryHash = "H1",
                QueryStoreEntry = new QueryStoreEntry { AvgCpuMs = 500, ExecutionCount = 10 }
            };

            var findings = new HighCpuRule().Evaluate(query, options).ToList();

            var finding = Assert.Single(findings);
            Assert.Equal("HIGHCPU001", finding.RuleId);
            Assert.Equal(Severity.Warning, finding.Severity);
        }

        [Fact]
        public void BelowThreshold_ProducesNoFinding()
        {
            var options = new QueryLensOptions();
            var query = new CorrelatedQuery
            {
                QueryHash = "H1",
                QueryStoreEntry = new QueryStoreEntry { AvgCpuMs = 5, ExecutionCount = 10 }
            };

            Assert.Empty(new HighCpuRule().Evaluate(query, options));
        }

        [Fact]
        public void NoQueryStoreData_ProducesNoFinding()
        {
            var options = new QueryLensOptions();
            var query = new CorrelatedQuery { QueryHash = "H1", ApplicationStatistics = new QueryStatistics { QueryHash = "H1" } };

            Assert.Empty(new HighCpuRule().Evaluate(query, options));
        }
    }

    public class HighLogicalReadsRuleTests
    {
        [Fact]
        public void AboveThreshold_ProducesFinding()
        {
            var options = new QueryLensOptions();
            var query = new CorrelatedQuery
            {
                QueryHash = "H1",
                QueryStoreEntry = new QueryStoreEntry { AvgLogicalReads = 50000, ExecutionCount = 10 }
            };

            var findings = new HighLogicalReadsRule().Evaluate(query, options).ToList();

            var finding = Assert.Single(findings);
            Assert.Equal("HIGHREADS001", finding.RuleId);
        }

        [Fact]
        public void BelowThreshold_ProducesNoFinding()
        {
            var options = new QueryLensOptions();
            var query = new CorrelatedQuery
            {
                QueryHash = "H1",
                QueryStoreEntry = new QueryStoreEntry { AvgLogicalReads = 100, ExecutionCount = 10 }
            };

            Assert.Empty(new HighLogicalReadsRule().Evaluate(query, options));
        }
    }

    public class QueryStoreRuleEngineTests
    {
        [Fact]
        public void Disabled_ProducesNoFindingsRegardlessOfThresholds()
        {
            var options = new QueryLensOptions();
            options.Analysis.QueryStore = false;
            var query = new CorrelatedQuery
            {
                QueryHash = "H1",
                QueryStoreEntry = new QueryStoreEntry { AvgCpuMs = 999, AvgLogicalReads = 999999, ExecutionCount = 1 }
            };

            var findings = QueryStoreRuleEngine.CreateDefault().Evaluate(new[] { query }, options);

            Assert.Empty(findings);
        }

        [Fact]
        public void Enabled_RunsBothDefaultRules()
        {
            var options = new QueryLensOptions();
            var query = new CorrelatedQuery
            {
                QueryHash = "H1",
                QueryStoreEntry = new QueryStoreEntry { AvgCpuMs = 999, AvgLogicalReads = 999999, ExecutionCount = 1 }
            };

            var findings = QueryStoreRuleEngine.CreateDefault().Evaluate(new[] { query }, options);

            Assert.Contains(findings, f => f.RuleId == "HIGHCPU001");
            Assert.Contains(findings, f => f.RuleId == "HIGHREADS001");
        }
    }
}
