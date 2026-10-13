using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Runs the configured execution-plan rules over one parsed plan.</summary>
    public sealed class ExecutionPlanRuleEngine
    {
        private readonly IReadOnlyList<IExecutionPlanRule> _rules;

        public ExecutionPlanRuleEngine(IEnumerable<IExecutionPlanRule> rules)
        {
            _rules = rules?.ToList() ?? new List<IExecutionPlanRule>();
        }

        public static ExecutionPlanRuleEngine CreateDefault() => new ExecutionPlanRuleEngine(new IExecutionPlanRule[]
        {
            new LargeScanRule(),
            new KeyLookupRule(),
            new ExpensiveSortRule(),
            new ExpensiveHashMatchRule(),
            new NestedLoopsRule(),
            new SpoolRule(),
            new ParallelismRule(),
            new RowCountDiscrepancyRule(),
            new MissingIndexRule()
        });

        /// <summary>Returns no findings if <see cref="AnalysisOptions.ExecutionPlan"/> is disabled.</summary>
        public IReadOnlyList<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            if (!options.Analysis.ExecutionPlan || plan == null)
            {
                return new List<Finding>();
            }

            var findings = new List<Finding>();
            foreach (var rule in _rules)
            {
                findings.AddRange(rule.Evaluate(plan, queryHash, options));
            }

            return findings;
        }
    }
}
