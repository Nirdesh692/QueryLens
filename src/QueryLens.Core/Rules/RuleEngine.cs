using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Runs the configured per-query and per-request rules over aggregated
    /// statistics and recent raw executions, producing the full set of
    /// findings. Rules are pluggable: pass a custom rule set to extend or
    /// replace the defaults.
    /// </summary>
    public sealed class RuleEngine
    {
        private readonly IReadOnlyList<IQueryAnalysisRule> _queryRules;
        private readonly IReadOnlyList<IRequestAnalysisRule> _requestRules;

        public RuleEngine(
            IEnumerable<IQueryAnalysisRule> queryRules,
            IEnumerable<IRequestAnalysisRule> requestRules)
        {
            _queryRules = queryRules?.ToList() ?? new List<IQueryAnalysisRule>();
            _requestRules = requestRules?.ToList() ?? new List<IRequestAnalysisRule>();
        }

        public static RuleEngine CreateDefault()
        {
            return new RuleEngine(
                new IQueryAnalysisRule[]
                {
                    new SlowQueryRule(),
                    new CriticalQueryRule(),
                    new LargeResultSetRule(),
                    new NPlusOneRule()
                },
                new IRequestAnalysisRule[]
                {
                    new QueryExplosionRule()
                });
        }

        /// <param name="statistics">Aggregated, rolled-up per-query statistics.</param>
        /// <param name="executions">
        /// Raw executions retained for request-scoped analysis (N+1, query
        /// explosion). Callers decide retention policy - see
        /// <see cref="Pipeline.QueryLensPipeline"/> for a bounded ring buffer.
        /// </param>
        /// <param name="options">Active configuration, including rule thresholds.</param>
        public IReadOnlyList<Finding> Evaluate(
            IReadOnlyCollection<QueryStatistics> statistics,
            IReadOnlyCollection<QueryExecution> executions,
            QueryLensOptions options)
        {
            var findings = new List<Finding>();
            executions = executions ?? new List<QueryExecution>();

            var executionsWithRequestId = executions.Where(e => e.RequestId != null).ToList();
            var executionsByQueryHash = executionsWithRequestId
                .GroupBy(e => e.QueryHash)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var stat in statistics)
            {
                var requestGroups = BuildRequestGroups(executionsByQueryHash, stat.QueryHash);
                var context = new RuleContext(stat, options, requestGroups);

                foreach (var rule in _queryRules)
                {
                    findings.AddRange(rule.Evaluate(context));
                }
            }

            var executionsByRequest = executionsWithRequestId.GroupBy(e => e.RequestId);

            foreach (var requestGroup in executionsByRequest)
            {
                var context = new RequestRuleContext(requestGroup.Key, requestGroup.ToList(), options);

                foreach (var rule in _requestRules)
                {
                    findings.AddRange(rule.Evaluate(context));
                }
            }

            return findings;
        }

        private static List<RequestQueryGroup> BuildRequestGroups(
            Dictionary<string, List<QueryExecution>> executionsByQueryHash,
            string queryHash)
        {
            if (queryHash == null || !executionsByQueryHash.TryGetValue(queryHash, out var executions))
            {
                return new List<RequestQueryGroup>();
            }

            return executions
                .GroupBy(e => e.RequestId)
                .Select(g => new RequestQueryGroup(g.Key, g.ToList()))
                .ToList();
        }
    }
}
