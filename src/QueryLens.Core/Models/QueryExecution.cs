using System;
using System.Collections.Generic;

namespace QueryLens.Core.Models
{
    /// <summary>
    /// A single captured database command execution, normalized into a common
    /// shape regardless of which capture adapter (EF6, EF Core, ADO.NET, Dapper)
    /// produced it. Metrics a given adapter or database cannot provide are left
    /// null rather than defaulted to zero, so downstream code can distinguish
    /// "zero" from "unknown".
    /// </summary>
    public sealed class QueryExecution
    {
        /// <summary>Raw SQL text as captured, before normalization.</summary>
        public string Sql { get; set; }

        /// <summary>Stable hash of the normalized SQL. Populated by the fingerprinter.</summary>
        public string QueryHash { get; set; }

        /// <summary>SQL with literal values replaced by placeholders. Populated by the fingerprinter.</summary>
        public string NormalizedSql { get; set; }

        /// <summary>Whitespace-cleaned SQL with original literal values retained, for display purposes.</summary>
        public string RepresentativeSql { get; set; }

        /// <summary>Capture source, e.g. "EF6", "EFCore", "AdoNet", "Dapper".</summary>
        public string Provider { get; set; }

        /// <summary>e.g. "Text" or "StoredProcedure", from the underlying ADO.NET command.</summary>
        public string CommandType { get; set; }

        public string Database { get; set; }

        public string Server { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public TimeSpan Duration { get; set; }

        public long? CpuTimeMs { get; set; }

        public long? LogicalReads { get; set; }

        public long? PhysicalReads { get; set; }

        public long? RowsReturned { get; set; }

        public long? RowsAffected { get; set; }

        /// <summary>Correlates queries issued within the same HTTP request/operation.</summary>
        public string RequestId { get; set; }

        public string CorrelationId { get; set; }

        /// <summary>Controller/endpoint or service method, where determinable.</summary>
        public string OperationName { get; set; }

        /// <summary>Source location (e.g. calling method), only set when reliably known.</summary>
        public string Source { get; set; }

        /// <summary>.NET Framework or .NET version the application is running on.</summary>
        public string Framework { get; set; }

        /// <summary>ORM in use, e.g. "EF6", "EFCore", "Dapper", "ADO.NET".</summary>
        public string Orm { get; set; }

        public string ApplicationName { get; set; }

        /// <summary>
        /// Parameter names/types/sizes only - never values. Null when the adapter
        /// did not attempt to collect parameters (see CaptureOptions.Parameters);
        /// an empty list when it looked and the command had none.
        /// </summary>
        public IReadOnlyList<QueryParameter> Parameters { get; set; }
    }
}
