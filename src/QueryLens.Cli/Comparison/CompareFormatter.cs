using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using QueryLens.Core.Baselines;

namespace QueryLens.Cli.Comparison
{
    /// <summary>Plain-text rendering of a <see cref="BaselineComparison"/>. Culture-invariant and deterministic.</summary>
    public static class CompareFormatter
    {
        public static string Format(BaselineComparison comparison)
        {
            var items = comparison.Items;
            var compared = items.Where(i => i.Status == QueryComparisonStatus.Compared)
                .OrderByDescending(i => i.AverageDurationChangePercent ?? double.NegativeInfinity)
                .ThenBy(i => i.QueryHash, StringComparer.Ordinal).ToList();
            var added = items.Where(i => i.Status == QueryComparisonStatus.New)
                .OrderBy(i => i.QueryHash, StringComparer.Ordinal).ToList();
            var removed = items.Where(i => i.Status == QueryComparisonStatus.Removed)
                .OrderBy(i => i.QueryHash, StringComparer.Ordinal).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Compared: {compared.Count}, New: {added.Count}, Removed: {removed.Count}");

            if (compared.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Changes (baseline -> current):");
                foreach (var item in compared)
                {
                    sb.AppendLine(
                        $"  {item.QueryHash}  avg {Percent(item.AverageDurationChangePercent)}  " +
                        $"p95 {Percent(item.P95DurationChangePercent)}  " +
                        $"cpu {Percent(item.CpuChangePercent)}  " +
                        $"reads {Percent(item.LogicalReadsChangePercent)}  " +
                        $"count {Percent(item.ExecutionCountChangePercent)}");
                    sb.AppendLine($"    {Truncate(item.NormalizedSql)}");
                }
            }

            AppendList(sb, "New queries:", added);
            AppendList(sb, "Removed queries:", removed);
            return sb.ToString();
        }

        public static string Percent(double? value)
        {
            if (!value.HasValue)
            {
                return "n/a";
            }

            return value.Value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static void AppendList(StringBuilder sb, string heading, List<QueryComparison> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine(heading);
            foreach (var item in items)
            {
                sb.AppendLine($"  {item.QueryHash}  {Truncate(item.NormalizedSql)}");
            }
        }

        private static string Truncate(string sql)
        {
            if (string.IsNullOrEmpty(sql))
            {
                return string.Empty;
            }

            return sql.Length <= 100 ? sql : sql.Substring(0, 97) + "...";
        }
    }
}
