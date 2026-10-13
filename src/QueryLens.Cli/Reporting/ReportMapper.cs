using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;

namespace QueryLens.Cli.Reporting
{
    /// <summary>Maps between Core report/statistics types and the JSON <see cref="ReportDocument"/>.</summary>
    public static class ReportMapper
    {
        public static ReportDocument ToDocument(PerformanceReport report, string label, DateTime generatedUtc)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var document = new ReportDocument
            {
                GeneratedUtc = generatedUtc,
                Label = label,
                Queries = (report.Statistics ?? new List<QueryStatistics>())
                    .OrderBy(s => s.QueryHash, StringComparer.Ordinal)
                    .Select(s => new QueryDocument
                    {
                        QueryHash = s.QueryHash,
                        NormalizedSql = s.NormalizedSql,
                        ExecutionCount = s.ExecutionCount,
                        AverageDurationMs = s.AverageDurationMs,
                        P95DurationMs = s.P95DurationMs,
                        MaxDurationMs = s.MaxDurationMs,
                        AverageCpuMs = s.AverageCpuMs,
                        LogicalReads = s.AverageLogicalReads
                    }).ToList(),
                Findings = (report.Findings ?? new List<Finding>())
                    .OrderBy(f => f.RuleId, StringComparer.Ordinal)
                    .ThenBy(f => f.QueryHash, StringComparer.Ordinal)
                    .Select(f => new FindingDocument
                    {
                        RuleId = f.RuleId,
                        Severity = f.Severity.ToString(),
                        Confidence = f.Confidence,
                        QueryHash = f.QueryHash,
                        Title = f.Title,
                        Description = f.Description,
                        Recommendation = f.Recommendation
                    }).ToList()
            };

            if (report.Score != null)
            {
                document.Score.Value = report.Score.Score;
                document.Score.Components = (report.Score.Components ?? new List<ScoreComponent>())
                    .Select(c => new ScoreComponentDocument { Name = c.Name, Penalty = c.Penalty, Reason = c.Reason })
                    .ToList();
            }

            return document;
        }

        /// <summary>Rebuilds statistics from a report. Fields the schema does not carry (e.g. P50) stay at zero.</summary>
        public static List<QueryStatistics> ToStatistics(ReportDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            return (document.Queries ?? new List<QueryDocument>())
                .Select(q => new QueryStatistics
                {
                    QueryHash = q.QueryHash,
                    NormalizedSql = q.NormalizedSql,
                    ExecutionCount = q.ExecutionCount,
                    AverageDurationMs = q.AverageDurationMs,
                    P95DurationMs = q.P95DurationMs,
                    MaxDurationMs = q.MaxDurationMs,
                    AverageCpuMs = q.AverageCpuMs,
                    AverageLogicalReads = q.LogicalReads
                }).ToList();
        }
    }
}
