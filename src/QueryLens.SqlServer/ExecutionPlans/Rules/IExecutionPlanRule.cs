using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;

namespace QueryLens.SqlServer.ExecutionPlans.Rules
{
    public interface IExecutionPlanRule
    {
        string RuleId { get; }

        IEnumerable<Finding> Evaluate(ExecutionPlan plan, string queryHash, QueryLensOptions options);
    }
}
