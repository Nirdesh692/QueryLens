using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.SqlServer.Correlation;

namespace QueryLens.SqlServer.Rules
{
    /// <summary>Runs the configured Query Store rules over a set of correlated queries.</summary>
    public sealed class QueryStoreRuleEngine
    {
        private readonly IReadOnlyList<IQueryStoreAnalysisRule> _rules;

        public QueryStoreRuleEngine(IEnumerable<IQueryStoreAnalysisRule> rules)
        {
            _rules = rules?.ToList() ?? new List<IQueryStoreAnalysisRule>();
        }

        public static QueryStoreRuleEngine CreateDefault() =>
            new QueryStoreRuleEngine(new IQueryStoreAnalysisRule[] { new HighCpuRule(), new HighLogicalReadsRule() });

        /// <summary>Returns no findings if <see cref="AnalysisOptions.QueryStore"/> is disabled.</summary>
        public IReadOnlyList<Finding> Evaluate(IEnumerable<CorrelatedQuery> correlatedQueries, QueryLensOptions options)
        {
            if (!options.Analysis.QueryStore)
            {
                return new List<Finding>();
            }

            var findings = new List<Finding>();

            foreach (var query in correlatedQueries ?? Enumerable.Empty<CorrelatedQuery>())
            {
                foreach (var rule in _rules)
                {
                    findings.AddRange(rule.Evaluate(query, options));
                }
            }

            return findings;
        }
    }
}
