using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Scoring
{
    public interface IPerformanceScorer
    {
        PerformanceScoreResult Score(IReadOnlyCollection<QueryStatistics> statistics, IReadOnlyList<Finding> findings);
    }
}
