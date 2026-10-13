using System.Collections.Generic;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.SqlServer.Correlation;

namespace QueryLens.SqlServer.Rules
{
    /// <summary>
    /// A rule that evaluates one correlated query using Query Store data
    /// (CPU, logical reads, etc.) that application-side capture alone can't
    /// see. Parallels QueryLens.Core.Rules.IQueryAnalysisRule, but scoped to
    /// this module since CorrelatedQuery is a SQL Server-specific concept.
    /// </summary>
    public interface IQueryStoreAnalysisRule
    {
        string RuleId { get; }

        IEnumerable<Finding> Evaluate(CorrelatedQuery query, QueryLensOptions options);
    }
}
