namespace QueryLens.Core.Configuration
{
    /// <summary>
    /// Root configuration for QueryLens. Every threshold used by the rule
    /// engine lives here rather than being hardcoded, per the design goal that
    /// nothing about "slow" or "too many" should be a magic number in code.
    /// </summary>
    public sealed class QueryLensOptions
    {
        public bool Enabled { get; set; } = true;

        public CaptureOptions Capture { get; set; } = new CaptureOptions();

        public ThresholdOptions Thresholds { get; set; } = new ThresholdOptions();

        public AnalysisOptions Analysis { get; set; } = new AnalysisOptions();

        public PrivacyOptions Privacy { get; set; } = new PrivacyOptions();
    }

    public sealed class CaptureOptions
    {
        public bool Sql { get; set; } = true;

        public bool Parameters { get; set; } = false;

        public bool Source { get; set; } = true;
    }

    public sealed class ThresholdOptions
    {
        public double SlowQueryMs { get; set; } = 500;

        public double CriticalQueryMs { get; set; } = 1000;

        public int MaxQueriesPerRequestWarning { get; set; } = 20;

        public int MaxQueriesPerRequestCritical { get; set; } = 50;

        public long MaxRowsReturned { get; set; } = 10000;

        /// <summary>Minimum repeats of a query within one request before N+1 is even considered.</summary>
        public int NPlusOneMinExecutions { get; set; } = 3;

        /// <summary>Minimum confidence (0.0-1.0) required before an N+1 finding is reported.</summary>
        public double NPlusOneMinConfidence { get; set; } = 0.6;

        /// <summary>Average CPU time (from Query Store), in milliseconds, above which a query is flagged as high-CPU.</summary>
        public double HighCpuMs { get; set; } = 200;

        /// <summary>Average logical reads (from Query Store) above which a query is flagged as high-IO.</summary>
        public long HighLogicalReads { get; set; } = 10000;

        /// <summary>Estimated rows (adjusted for repeat executions) above which a Table/Clustered Index/Index Scan is flagged.</summary>
        public double LargeScanRowThreshold { get; set; } = 10000;

        /// <summary>Estimated rows above which a Key Lookup / RID Lookup is flagged.</summary>
        public double KeyLookupRowThreshold { get; set; } = 1000;

        /// <summary>Estimated rows above which a Sort operator is flagged.</summary>
        public double ExpensiveSortRowThreshold { get; set; } = 10000;

        /// <summary>Estimated rows above which a Hash Match operator is flagged.</summary>
        public double ExpensiveHashRowThreshold { get; set; } = 10000;

        /// <summary>Estimated rows above which a Spool operator (Table/Index/Row Count/Window Spool) is flagged.</summary>
        public double SpoolRowThreshold { get; set; } = 10000;

        /// <summary>
        /// How many times larger the bigger of (estimated, actual) rows must be than the
        /// smaller before a cardinality-estimate discrepancy is flagged (e.g. 10 = 10x off).
        /// Only evaluated when an actual execution plan (not just an estimated one) is available.
        /// </summary>
        public double RowCountDiscrepancyRatio { get; set; } = 10.0;

        /// <summary>Percentage increase over the baseline (e.g. 20 = 20% worse) at which a metric is flagged as regressed.</summary>
        public double RegressionTolerancePercent { get; set; } = 20;

        /// <summary>Percentage increase over the baseline at which a regression is reported as Critical instead of Warning.</summary>
        public double RegressionCriticalPercent { get; set; } = 100;

        /// <summary>Baseline average duration/CPU (ms) below which changes are ignored as noise.</summary>
        public double RegressionMinBaselineMs { get; set; } = 1;
    }

    public sealed class AnalysisOptions
    {
        public bool NPlusOne { get; set; } = true;

        public bool QueryRegression { get; set; } = true;

        public bool ExecutionPlan { get; set; } = true;

        public bool QueryStore { get; set; } = true;
    }

    public sealed class PrivacyOptions
    {
        /// <summary>Off by default: parameter values are not captured unless explicitly enabled.</summary>
        public bool CaptureParameterValues { get; set; } = false;

        public bool RedactSensitiveData { get; set; } = true;
    }
}
