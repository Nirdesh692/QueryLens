using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>A rule that evaluates one query's aggregated statistics.</summary>
    public interface IQueryAnalysisRule
    {
        string RuleId { get; }

        IEnumerable<Finding> Evaluate(RuleContext context);
    }
}
