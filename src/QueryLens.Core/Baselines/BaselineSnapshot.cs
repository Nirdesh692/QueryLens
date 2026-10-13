using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;
using QueryLens.Core.Pipeline;

namespace QueryLens.Core.Baselines
{
    /// <summary>
    /// A point-in-time copy of per-query statistics, used as the reference
    /// that later runs are compared against to detect regressions.
    /// </summary>
    public sealed class BaselineSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Free-form label, e.g. a release number or commit hash.</summary>
        public string Label { get; set; }

        public List<QueryStatistics> Queries { get; set; } = new List<QueryStatistics>();

        public static BaselineSnapshot FromStatistics(IEnumerable<QueryStatistics> statistics, string label = null)
        {
            return new BaselineSnapshot
            {
                Label = label,
                Queries = (statistics ?? Enumerable.Empty<QueryStatistics>()).ToList()
            };
        }

        public static BaselineSnapshot FromReport(PerformanceReport report, string label = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return FromStatistics(report.Statistics, label);
        }
    }
}
