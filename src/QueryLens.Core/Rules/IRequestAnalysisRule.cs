using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>A rule that evaluates all queries within one request/operation.</summary>
    public interface IRequestAnalysisRule
    {
        string RuleId { get; }

        IEnumerable<Finding> Evaluate(RequestRuleContext context);
    }
}
