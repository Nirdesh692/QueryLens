using System.Collections.Generic;

namespace QueryLens.Core.Models
{
    /// <summary>
    /// One category's contribution to the overall performance score. The score
    /// is never returned without this breakdown - see the scoring design note
    /// in the product spec: "Never give a score without explaining why."
    /// </summary>
    public sealed class ScoreComponent
    {
        public string Name { get; set; }

        /// <summary>Negative or zero - the points deducted for this category.</summary>
        public double Penalty { get; set; }

        public string Reason { get; set; }
    }

    public sealed class PerformanceScoreResult
    {
        /// <summary>0-100.</summary>
        public int Score { get; set; }

        public IReadOnlyList<ScoreComponent> Components { get; set; } = new List<ScoreComponent>();
    }
}
