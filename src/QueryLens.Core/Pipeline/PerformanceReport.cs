using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Pipeline
{
    /// <summary>Result of running the rule engine and scorer over current statistics.</summary>
    public sealed class PerformanceReport
    {
        public PerformanceReport(
            IReadOnlyCollection<QueryStatistics> statistics,
            IReadOnlyList<Finding> findings,
            PerformanceScoreResult score)
        {
            Statistics = statistics;
            Findings = findings;
            Score = score;
        }

        public IReadOnlyCollection<QueryStatistics> Statistics { get; }

        public IReadOnlyList<Finding> Findings { get; }

        public PerformanceScoreResult Score { get; }
    }
}
