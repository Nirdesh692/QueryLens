using System;
using System.Collections.Generic;

namespace QueryLens.SqlServer.ExecutionPlans
{
    /// <summary>SQL Server's own missing-index suggestion, if the plan included one.</summary>
    public sealed class MissingIndexSuggestion
    {
        public string Database { get; set; }

        public string Schema { get; set; }

        public string Table { get; set; }

        /// <summary>SQL Server's estimated percentage improvement (0-100-ish; can exceed 100 in rare cases).</summary>
        public double Impact { get; set; }

        public IReadOnlyList<string> EqualityColumns { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> InequalityColumns { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> IncludedColumns { get; set; } = Array.Empty<string>();
    }
}
