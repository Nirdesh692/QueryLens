using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using QueryLens.Core.Aggregation;
using QueryLens.Core.Baselines;
using QueryLens.Core.Configuration;
using QueryLens.Core.Fingerprinting;
using QueryLens.Core.Interfaces;
using QueryLens.Core.Models;
using QueryLens.Core.Rules;
using QueryLens.Core.Scoring;

namespace QueryLens.Core.Pipeline
{
    /// <summary>
    /// Wires together fingerprinting, aggregation, the rule engine, and scoring
    /// into a single entry point. Capture adapters (EF6, EF Core, ADO.NET,
    /// Dapper, etc. - added in later phases) call <see cref="CaptureAsync"/>
    /// for every observed database command; everything downstream here is
    /// adapter-agnostic and has no dependency on any ORM.
    /// </summary>
    public sealed class QueryLensPipeline
    {
        private readonly IQueryFingerprinter _fingerprinter;
        private readonly IQueryAggregator _aggregator;
        private readonly RuleEngine _ruleEngine;
        private readonly IPerformanceScorer _scorer;
        private readonly IQueryMetricsSink _sink;
        private readonly QueryLensOptions _options;

        private readonly List<QueryExecution> _recentExecutions = new List<QueryExecution>();
        private readonly object _recentExecutionsLock = new object();
        private readonly int _maxRecentExecutions;

        public QueryLensPipeline(
            QueryLensOptions options = null,
            IQueryFingerprinter fingerprinter = null,
            IQueryAggregator aggregator = null,
            RuleEngine ruleEngine = null,
            IPerformanceScorer scorer = null,
            IQueryMetricsSink sink = null,
            int maxRecentExecutions = 10000)
        {
            _options = options ?? new QueryLensOptions();
            _fingerprinter = fingerprinter ?? new SqlQueryFingerprinter();
            _aggregator = aggregator ?? new QueryAggregator();
            _ruleEngine = ruleEngine ?? RuleEngine.CreateDefault();
            _scorer = scorer ?? new PerformanceScorer();
            _sink = sink;
            _maxRecentExecutions = maxRecentExecutions;
        }

        /// <summary>Active configuration, exposed so capture adapters can check settings like Capture.Parameters.</summary>
        public QueryLensOptions Options => _options;

        /// <summary>
        /// Fingerprints (if not already fingerprinted), aggregates, and
        /// optionally forwards one captured database command to the
        /// configured sink. Intended to be safe to call from the database
        /// command interception path - no blocking I/O occurs here beyond the
        /// sink write, which adapters should keep non-blocking.
        /// </summary>
        public async Task<QueryExecution> CaptureAsync(QueryExecution execution, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return execution;
            }

            if (execution.NormalizedSql == null && execution.Sql != null)
            {
                var fingerprint = _fingerprinter.Fingerprint(execution.Sql);
                execution.QueryHash = fingerprint.Hash;
                execution.NormalizedSql = fingerprint.NormalizedSql;
                execution.RepresentativeSql = fingerprint.RepresentativeSql;
            }

            _aggregator.Add(execution);
            TrackRecent(execution);

            if (_sink != null)
            {
                await _sink.WriteAsync(execution, cancellationToken).ConfigureAwait(false);
            }

            return execution;
        }

        /// <summary>Runs the rule engine and scorer over everything aggregated so far.</summary>
        public PerformanceReport Analyze()
        {
            var statistics = _aggregator.GetAllStatistics();

            List<QueryExecution> executionsSnapshot;
            lock (_recentExecutionsLock)
            {
                executionsSnapshot = new List<QueryExecution>(_recentExecutions);
            }

            var findings = _ruleEngine.Evaluate(statistics, executionsSnapshot, _options);
            var score = _scorer.Score(statistics, findings);

            return new PerformanceReport(statistics, findings, score);
        }

        /// <summary>Snapshots the statistics aggregated so far for use as a future regression baseline.</summary>
        public BaselineSnapshot CreateBaseline(string label = null)
        {
            return BaselineSnapshot.FromStatistics(_aggregator.GetAllStatistics(), label);
        }

        /// <summary>
        /// Runs <see cref="Analyze"/>, then compares against <paramref name="baseline"/> and
        /// merges any QUERYREGRESSION001 findings into the findings and score.
        /// </summary>
        public PerformanceReport AnalyzeAgainstBaseline(BaselineSnapshot baseline)
        {
            var report = Analyze();

            var comparison = new BaselineComparer().Compare(baseline, report.Statistics);
            var regressions = new QueryRegressionRule().Evaluate(comparison, _options);

            if (regressions.Count == 0)
            {
                return report;
            }

            var findings = report.Findings.Concat(regressions).ToList();
            return new PerformanceReport(report.Statistics, findings, _scorer.Score(report.Statistics, findings));
        }

        private void TrackRecent(QueryExecution execution)
        {
            lock (_recentExecutionsLock)
            {
                _recentExecutions.Add(execution);
                if (_recentExecutions.Count > _maxRecentExecutions)
                {
                    _recentExecutions.RemoveAt(0);
                }
            }
        }
    }
}
