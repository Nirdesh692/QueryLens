using System;
using System.Collections.Generic;

namespace QueryLens.SqlServer.ExecutionPlans
{
    /// <summary>
    /// One operator (RelOp) node from a SQL Server execution plan.
    /// </summary>
    public sealed class PlanOperator
    {
        public string PhysicalOp { get; set; }

        public string LogicalOp { get; set; }

        /// <summary>Estimated rows for a single execution of this operator.</summary>
        public double EstimateRows { get; set; }

        /// <summary>Estimated number of times this operator executes (e.g. the inner side of a nested loop). Defaults to 1.</summary>
        public double EstimateExecutions { get; set; } = 1;

        /// <summary>EstimateRows * EstimateExecutions - the total rows this operator is expected to process, accounting for repeat execution.</summary>
        public double EffectiveEstimatedRows => EstimateRows * Math.Max(EstimateExecutions, 1);

        public double EstimatedTotalSubtreeCost { get; set; }

        /// <summary>
        /// Actual rows produced, summed across threads - only present when this plan
        /// came from an *actual* execution plan (with runtime statistics), not a
        /// compile-time estimated one. Query Store plans are typically estimated-only.
        /// </summary>
        public double? ActualRows { get; set; }

        public bool Parallel { get; set; }

        /// <summary>Table (and index, if applicable) this operator touches, e.g. "Orders.IX_Orders_CustomerId". Null if not applicable.</summary>
        public string ObjectName { get; set; }

        public IReadOnlyList<PlanOperator> Children { get; set; } = Array.Empty<PlanOperator>();
    }
}
