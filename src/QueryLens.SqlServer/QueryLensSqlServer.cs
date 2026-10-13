using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;
using QueryLens.Core.Scoring;
using QueryLens.SqlServer.Correlation;
using QueryLens.SqlServer.ExecutionPlans;
using QueryLens.SqlServer.ExecutionPlans.Rules;
using QueryLens.SqlServer.QueryStore;
using QueryLens.SqlServer.Rules;

namespace QueryLens.SqlServer
{
    /// <summary>Result of combining application-side capture with Query Store and execution-plan data.</summary>
    public sealed class QueryStoreReport
    {
        public QueryStoreReport(
            IReadOnlyList<CorrelatedQuery> correlatedQueries,
            IReadOnlyList<Finding> findings,
            PerformanceScoreResult score)
        {
            CorrelatedQueries = correlatedQueries;
            Findings = findings;
            Score = score;
        }

        public IReadOnlyList<CorrelatedQuery> CorrelatedQueries { get; }

        /// <summary>Every finding - from Core's rules, Query Store's CPU/reads rules, and execution-plan rules, all merged.</summary>
        public IReadOnlyList<Finding> Findings { get; }

        /// <summary>Score recomputed over the combined finding set.</summary>
        public PerformanceScoreResult Score { get; }
    }

    /// <summary>
    /// Entry point for enriching a QueryLens report with SQL Server Query Store
    /// data (and, where a plan is available, execution-plan analysis): run your
    /// app with any capture adapter as usual, then call
    /// <see cref="AnalyzeWithQueryStoreAsync"/> with a connection to the same
    /// database to correlate application-side timing with what SQL Server
    /// itself recorded.
    /// </summary>
    public static class QueryLensSqlServer
    {
        public static async Task<QueryStoreReport> AnalyzeWithQueryStoreAsync(
            QueryLensPipeline pipeline,
            DbConnection connection,
            QueryStoreReadOptions options = null,
            IQueryStoreReader reader = null,
            IQueryStoreCorrelator correlator = null,
            IExecutionPlanParser planParser = null,
            CancellationToken cancellationToken = default)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            reader = reader ?? new QueryStoreReader();
            correlator = correlator ?? new QueryStoreCorrelator();
            planParser = planParser ?? new ExecutionPlanParser();

            var appReport = pipeline.Analyze();
            var entries = await reader.ReadAsync(connection, options, cancellationToken).ConfigureAwait(false);
            var correlated = correlator.Correlate(appReport.Statistics, entries);

            var queryStoreFindings = QueryStoreRuleEngine.CreateDefault().Evaluate(correlated, pipeline.Options);
            var executionPlanFindings = new List<Finding>();

            if (pipeline.Options.Analysis.ExecutionPlan)
            {
                var planRuleEngine = ExecutionPlanRuleEngine.CreateDefault();

                foreach (var query in correlated)
                {
                    var planXml = query.QueryStoreEntry?.ExecutionPlanXml;
                    if (string.IsNullOrEmpty(planXml))
                    {
                        continue;
                    }

                    ExecutionPlan plan;
                    try
                    {
                        plan = planParser.Parse(planXml);
                    }
                    catch (ExecutionPlanParseException)
                    {
                        // A plan that fails to parse shouldn't break the whole
                        // report - skip plan-level analysis for this one query.
                        continue;
                    }

                    query.ParsedExecutionPlan = plan;
                    executionPlanFindings.AddRange(planRuleEngine.Evaluate(plan, query.QueryHash, pipeline.Options));
                }
            }

            var allFindings = appReport.Findings
                .Concat(queryStoreFindings)
                .Concat(executionPlanFindings)
                .ToList();

            var score = new PerformanceScorer().Score(appReport.Statistics, allFindings);

            return new QueryStoreReport(correlated, allFindings, score);
        }
    }
}
