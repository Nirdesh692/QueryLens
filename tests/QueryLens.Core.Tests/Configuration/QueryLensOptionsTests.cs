using QueryLens.Core.Configuration;
using Xunit;

namespace QueryLens.Core.Tests.Configuration
{
    public class QueryLensOptionsTests
    {
        [Fact]
        public void DefaultThresholds_MatchDocumentedDefaults()
        {
            var options = new QueryLensOptions();

            Assert.True(options.Enabled);
            Assert.Equal(500, options.Thresholds.SlowQueryMs);
            Assert.Equal(1000, options.Thresholds.CriticalQueryMs);
            Assert.Equal(20, options.Thresholds.MaxQueriesPerRequestWarning);
            Assert.Equal(50, options.Thresholds.MaxQueriesPerRequestCritical);
            Assert.Equal(10000, options.Thresholds.MaxRowsReturned);
            Assert.False(options.Privacy.CaptureParameterValues);
            Assert.True(options.Privacy.RedactSensitiveData);
            Assert.True(options.Analysis.NPlusOne);
            Assert.True(options.Analysis.QueryStore);
            Assert.Equal(200, options.Thresholds.HighCpuMs);
            Assert.Equal(10000, options.Thresholds.HighLogicalReads);
            Assert.Equal(10000, options.Thresholds.LargeScanRowThreshold);
            Assert.Equal(1000, options.Thresholds.KeyLookupRowThreshold);
            Assert.Equal(10000, options.Thresholds.ExpensiveSortRowThreshold);
            Assert.Equal(10000, options.Thresholds.ExpensiveHashRowThreshold);
            Assert.Equal(10000, options.Thresholds.SpoolRowThreshold);
            Assert.Equal(10.0, options.Thresholds.RowCountDiscrepancyRatio);
            Assert.True(options.Analysis.ExecutionPlan);
        }
    }
}
