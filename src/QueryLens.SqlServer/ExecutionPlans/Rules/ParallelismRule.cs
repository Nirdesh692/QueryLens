using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    /// <summary>Informational: notes when a plan used parallelism. Not inherently a problem.</summary>
    public sealed class ParallelismRule : IExecutionPlanRule
    {
        public string RuleId => "PARALLELISM001";

        public IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options)
        {
            var usesParallelism = (plan?.AllOperators ?? Enumerable.Empty<PlanOperator>()).Any(op => op.Parallel);
            if (!usesParallelism)
            {
                yield break;
            }

            yield return new Finding
            {
                RuleId = RuleId,
                Severity = Severity.Info,
                Title = "Plan uses parallelism",
                Description = "This plan includes one or more parallel operators.",
                QueryHash = queryHash,
                Confidence = 1.0,
                Recommendation = "Not necessarily a problem - parallelism often helps large queries - but worth knowing if you're investigating unexpectedly high CPU relative to duration.",
                Evidence = new Dictionary<string, string>()
            };
        }
    }
}
