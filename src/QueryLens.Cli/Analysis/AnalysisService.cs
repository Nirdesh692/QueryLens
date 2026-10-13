using System.Collections.Generic;
using QueryLens.Core.Baselines;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;
using QueryLens.Core.Rules;
using QueryLens.Core.Scoring;

namespace QueryLens.Cli.Analysis
{
    /// <summary>
    /// Runs the Core rule engine and scorer over stored statistics. Raw
    /// executions are not stored, so request-scoped rules (N+1, query
    /// explosion) cannot fire here.
    /// </summary>
    public static class AnalysisService
    {
        public static PerformanceReport Analyze(
            IReadOnlyCollection<QueryStatistics> statistics,
            BaselineSnapshot baseline = null,
            QueryLensOptions options = null)
        {
            options = options ?? new QueryLensOptions();
            var scorer = new PerformanceScorer();

            var findings = new List<Finding>(
                RuleEngine.CreateDefault().Evaluate(statistics, new List<QueryExecution>(), options));

            if (baseline != null && options.Analysis.QueryRegression)
            {
                var comparison = new BaselineComparer().Compare(baseline, statistics);
                findings.AddRange(new QueryRegressionRule().Evaluate(comparison, options));
            }

            return new PerformanceReport(statistics, findings, scorer.Score(statistics, findings));
        }
    }
}
