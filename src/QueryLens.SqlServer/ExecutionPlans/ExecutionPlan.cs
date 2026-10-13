using System;
using System.Collections.Generic;

namespace QueryLens.SqlServer.ExecutionPlans
{
    /// <summary>A parsed execution plan: the operator tree plus any missing-index suggestions.</summary>
    public sealed class ExecutionPlan
    {
        public PlanOperator Root { get; set; }

        /// <summary>Every operator in the plan, flattened in document (pre-order) order.</summary>
        public IReadOnlyList<PlanOperator> AllOperators { get; set; } = Array.Empty<PlanOperator>();

        public IReadOnlyList<MissingIndexSuggestion> MissingIndexSuggestions { get; set; } = Array.Empty<MissingIndexSuggestion>();
    }
}
