using System;
using System.Collections.Generic;

namespace QueryLens.Cli.Reporting
{
    /// <summary>
    /// Stable, versioned JSON shape of a performance report (camelCase on the wire).
    /// Additive changes keep <see cref="CurrentSchemaVersion"/>; breaking changes bump it.
    /// Contains no parameter values.
    /// </summary>
    public sealed class ReportDocument
    {
        public const int CurrentSchemaVersion = 1;

        public int ReportSchemaVersion { get; set; } = CurrentSchemaVersion;

        public string Tool { get; set; } = "querylens";

        public DateTime GeneratedUtc { get; set; }

        public string Label { get; set; }

        public ScoreDocument Score { get; set; } = new ScoreDocument();

        public List<QueryDocument> Queries { get; set; } = new List<QueryDocument>();

        public List<FindingDocument> Findings { get; set; } = new List<FindingDocument>();
    }

    public sealed class ScoreDocument
    {
        public int Value { get; set; }

        public List<ScoreComponentDocument> Components { get; set; } = new List<ScoreComponentDocument>();
    }

    public sealed class ScoreComponentDocument
    {
        public string Name { get; set; }

        public double Penalty { get; set; }

        public string Reason { get; set; }
    }

    public sealed class QueryDocument
    {
        public string QueryHash { get; set; }

        public string NormalizedSql { get; set; }

        public long ExecutionCount { get; set; }

        public double AverageDurationMs { get; set; }

        public double P95DurationMs { get; set; }

        public double MaxDurationMs { get; set; }

        public double? AverageCpuMs { get; set; }

        /// <summary>Average logical reads per execution, when known.</summary>
        public double? LogicalReads { get; set; }
    }

    public sealed class FindingDocument
    {
        public string RuleId { get; set; }

        /// <summary>"Info", "Warning" or "Critical".</summary>
        public string Severity { get; set; }

        public double Confidence { get; set; }

        public string QueryHash { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Recommendation { get; set; }
    }
}
