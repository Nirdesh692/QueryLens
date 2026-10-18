using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace QueryLens.Cli.Reporting
{
    /// <summary>
    /// Renders a self-contained, server-less HTML performance report with embedded styling,
    /// executive scorecard, diagnostic callouts, top query rankings, and full query details.
    /// </summary>
    public sealed class HtmlReportRenderer : IReportRenderer
    {
        public string Format => "html";

        public string Render(ReportDocument report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var sb = new StringBuilder(16384);
            var ci = CultureInfo.InvariantCulture;

            var score = report.Score?.Value ?? 0;
            var scoreColorClass = score >= 80 ? "score-good" : (score >= 60 ? "score-warn" : "score-bad");
            var scoreBgClass = score >= 80 ? "bg-good" : (score >= 60 ? "bg-warn" : "bg-bad");

            var queries = report.Queries ?? new List<QueryDocument>();
            var findings = report.Findings ?? new List<FindingDocument>();
            var components = report.Score?.Components ?? new List<ScoreComponentDocument>();

            long totalExecutions = 0;
            foreach (var q in queries)
            {
                totalExecutions += q.ExecutionCount;
            }

            int criticalCount = 0;
            int warningCount = 0;
            int infoCount = 0;
            foreach (var f in findings)
            {
                if (string.Equals(f.Severity, "Critical", StringComparison.OrdinalIgnoreCase)) criticalCount++;
                else if (string.Equals(f.Severity, "Warning", StringComparison.OrdinalIgnoreCase)) warningCount++;
                else infoCount++;
            }

            // Top queries
            var topSlow = queries.OrderByDescending(q => q.AverageDurationMs).Take(5).ToList();
            var topCpu = queries.Where(q => q.AverageCpuMs.HasValue).OrderByDescending(q => q.AverageCpuMs.Value).Take(5).ToList();
            var topReads = queries.Where(q => q.LogicalReads.HasValue).OrderByDescending(q => q.LogicalReads.Value).Take(5).ToList();

            // Special categories
            var regressionFindings = findings.Where(f => string.Equals(f.RuleId, "QUERYREGRESSION001", StringComparison.OrdinalIgnoreCase)).ToList();
            var nplusOneFindings = findings.Where(f => string.Equals(f.RuleId, "NPLUSONE001", StringComparison.OrdinalIgnoreCase)).ToList();
            var explosionFindings = findings.Where(f => string.Equals(f.RuleId, "QUERYEXPLOSION001", StringComparison.OrdinalIgnoreCase)).ToList();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"utf-8\" />");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
            sb.Append("  <title>QueryLens Performance Report");
            if (!string.IsNullOrWhiteSpace(report.Label))
            {
                sb.Append(" - ");
                sb.Append(WebUtility.HtmlEncode(report.Label));
            }
            sb.AppendLine("</title>");

            sb.AppendLine("  <style>");
            sb.AppendLine(@"    :root {
      --bg: #f8fafc;
      --card-bg: #ffffff;
      --card-border: #e2e8f0;
      --text: #0f172a;
      --text-muted: #64748b;
      --primary: #2563eb;
      --table-header: #f1f5f9;
      --table-row-hover: #f8fafc;
      --code-bg: #0f172a;
      --code-text: #e2e8f0;
      --color-good: #16a34a;
      --color-good-bg: #dcfce7;
      --color-warn: #d97706;
      --color-warn-bg: #fef3c7;
      --color-bad: #dc2626;
      --color-bad-bg: #fee2e2;
      --color-info: #0284c7;
      --color-info-bg: #e0f2fe;
    }
    @media (prefers-color-scheme: dark) {
      :root {
        --bg: #0b0f19;
        --card-bg: #131b2e;
        --card-border: #1e293b;
        --text: #f1f5f9;
        --text-muted: #94a3b8;
        --primary: #3b82f6;
        --table-header: #1a243b;
        --table-row-hover: #1e293b;
        --code-bg: #050811;
        --code-text: #e2e8f0;
        --color-good: #22c55e;
        --color-good-bg: rgba(34, 197, 94, 0.15);
        --color-warn: #f59e0b;
        --color-warn-bg: rgba(245, 158, 11, 0.15);
        --color-bad: #ef4444;
        --color-bad-bg: rgba(239, 68, 68, 0.15);
        --color-info: #38bdf8;
        --color-info-bg: rgba(56, 189, 248, 0.15);
      }
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
      background-color: var(--bg);
      color: var(--text);
      line-height: 1.5;
      padding: 2rem 1.5rem;
    }
    .container { max-width: 1200px; margin: 0 auto; }
    header {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid var(--card-border);
    }
    .brand { font-size: 1.6rem; font-weight: 700; color: var(--primary); letter-spacing: -0.02em; }
    .meta { font-size: 0.875rem; color: var(--text-muted); text-align: right; }
    .meta span { display: block; }
    
    .grid { display: grid; gap: 1.5rem; margin-bottom: 2rem; }
    .grid-2 { grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); }
    .grid-3 { grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
    .grid-4 { grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }

    .card {
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 8px;
      padding: 1.25rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }
    .card-title {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 0.75rem;
    }

    .score-container {
      display: flex;
      align-items: center;
      gap: 1.5rem;
    }
    .score-badge {
      font-size: 3rem;
      font-weight: 800;
      width: 110px;
      height: 110px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .score-good { color: var(--color-good); }
    .score-warn { color: var(--color-warn); }
    .score-bad { color: var(--color-bad); }
    .bg-good { background: var(--color-good-bg); border: 2px solid var(--color-good); }
    .bg-warn { background: var(--color-warn-bg); border: 2px solid var(--color-warn); }
    .bg-bad { background: var(--color-bad-bg); border: 2px solid var(--color-bad); }

    .kpi-value { font-size: 2rem; font-weight: 700; color: var(--text); }
    .kpi-sub { font-size: 0.85rem; color: var(--text-muted); margin-top: 0.25rem; }

    .callout {
      border-left: 4px solid var(--color-warn);
      background: var(--color-warn-bg);
      padding: 1rem 1.25rem;
      border-radius: 0 6px 6px 0;
      margin-bottom: 1.5rem;
    }
    .callout-danger {
      border-left-color: var(--color-bad);
      background: var(--color-bad-bg);
    }
    .callout-title { font-weight: 700; margin-bottom: 0.5rem; }

    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }
    th, td {
      padding: 0.75rem 1rem;
      text-align: left;
      border-bottom: 1px solid var(--card-border);
    }
    th {
      background: var(--table-header);
      color: var(--text-muted);
      font-weight: 600;
      text-transform: uppercase;
      font-size: 0.75rem;
      letter-spacing: 0.05em;
    }
    tr:hover td { background: var(--table-row-hover); }
    .text-right { text-align: right; }
    .text-center { text-align: center; }

    .badge {
      display: inline-block;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
    }
    .badge-critical { background: var(--color-bad-bg); color: var(--color-bad); }
    .badge-warning { background: var(--color-warn-bg); color: var(--color-warn); }
    .badge-info { background: var(--color-info-bg); color: var(--color-info); }

    .query-hash {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      font-weight: 600;
      color: var(--primary);
      text-decoration: none;
    }
    .query-hash:hover { text-decoration: underline; }

    pre code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      font-size: 0.85rem;
      background: var(--code-bg);
      color: var(--code-text);
      padding: 0.75rem 1rem;
      border-radius: 6px;
      display: block;
      overflow-x: auto;
      white-space: pre-wrap;
      word-break: break-all;
      margin-top: 0.5rem;
    }
    .section-title {
      font-size: 1.25rem;
      font-weight: 700;
      margin: 2rem 0 1rem 0;
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .recommendation-box {
      margin-top: 0.5rem;
      padding: 0.5rem 0.75rem;
      background: var(--bg);
      border-radius: 4px;
      font-size: 0.85rem;
      color: var(--text);
      border-left: 3px solid var(--primary);
    }
    footer {
      margin-top: 3rem;
      padding-top: 1.5rem;
      border-top: 1px solid var(--card-border);
      text-align: center;
      font-size: 0.85rem;
      color: var(--text-muted);
    }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"container\">");

            // Header
            sb.AppendLine("    <header>");
            sb.AppendLine("      <div>");
            sb.AppendLine("        <div class=\"brand\">QueryLens</div>");
            sb.Append("        <p style=\"color: var(--text-muted); font-size: 0.9rem;\">Database Performance Report");
            if (!string.IsNullOrWhiteSpace(report.Label))
            {
                sb.Append(" &mdash; <strong>");
                sb.Append(WebUtility.HtmlEncode(report.Label));
                sb.Append("</strong>");
            }
            sb.AppendLine("</p>");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div class=\"meta\">");
            sb.AppendLine($"        <span>Generated: <strong>{report.GeneratedUtc:yyyy-MM-dd HH:mm:ss} UTC</strong></span>");
            sb.AppendLine($"        <span>Schema Version: <strong>{report.ReportSchemaVersion}</strong></span>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </header>");

            // Score and Score Components
            sb.AppendLine("    <div class=\"grid grid-2\">");
            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine("        <div class=\"card-title\">Performance Score</div>");
            sb.AppendLine("        <div class=\"score-container\">");
            sb.AppendLine($"          <div class=\"score-badge {scoreColorClass} {scoreBgClass}\">{score}</div>");
            sb.AppendLine("          <div>");
            sb.AppendLine($"            <h3 style=\"font-size: 1.25rem; font-weight: 700; margin-bottom: 0.25rem;\">{score} / 100</h3>");
            sb.AppendLine($"            <p style=\"font-size: 0.9rem; color: var(--text-muted);\">{(score >= 80 ? "Healthy workload performance." : (score >= 60 ? "Performance issues detected requiring review." : "Critical performance issues require immediate attention."))}</p>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine("        <div class=\"card-title\">Score Breakdown & Penalties</div>");
            if (components.Count == 0 || components.All(c => c.Penalty == 0))
            {
                sb.AppendLine("        <p style=\"font-size: 0.9rem; color: var(--color-good); margin-top: 1rem;\">No score deductions applied.</p>");
            }
            else
            {
                sb.AppendLine("        <table>");
                sb.AppendLine("          <thead>");
                sb.AppendLine("            <tr><th>Category</th><th class=\"text-right\">Penalty</th><th>Reason</th></tr>");
                sb.AppendLine("          </thead>");
                sb.AppendLine("          <tbody>");
                foreach (var c in components.Where(x => x.Penalty > 0))
                {
                    sb.AppendLine("            <tr>");
                    sb.AppendLine($"              <td><strong>{WebUtility.HtmlEncode(c.Name)}</strong></td>");
                    sb.AppendLine($"              <td class=\"text-right score-bad\">-{c.Penalty.ToString("0.#", ci)}</td>");
                    sb.AppendLine($"              <td style=\"color: var(--text-muted); font-size: 0.85rem;\">{WebUtility.HtmlEncode(c.Reason ?? string.Empty)}</td>");
                    sb.AppendLine("            </tr>");
                }
                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");
            }
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");

            // KPIs
            sb.AppendLine("    <div class=\"grid grid-4\">");
            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine("        <div class=\"card-title\">Queries</div>");
            sb.AppendLine($"        <div class=\"kpi-value\">{queries.Count}</div>");
            sb.AppendLine($"        <div class=\"kpi-sub\">{totalExecutions.ToString("N0", ci)} executions</div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine("        <div class=\"card-title\">Critical Issues</div>");
            sb.AppendLine($"        <div class=\"kpi-value {(criticalCount > 0 ? "score-bad" : string.Empty)}\">{criticalCount}</div>");
            sb.AppendLine("        <div class=\"kpi-sub\">Immediate action needed</div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine("        <div class=\"card-title\">Warnings</div>");
            sb.AppendLine($"        <div class=\"kpi-value {(warningCount > 0 ? "score-warn" : string.Empty)}\">{warningCount}</div>");
            sb.AppendLine("        <div class=\"kpi-sub\">Potential bottlenecks</div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div class=\"card\">");
            sb.AppendLine("        <div class=\"card-title\">Informational</div>");
            sb.AppendLine($"        <div class=\"kpi-value\">{infoCount}</div>");
            sb.AppendLine("        <div class=\"kpi-sub\">Optimization leads</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");

            // Diagnostic Callouts (Regressions, N+1, Explosion)
            if (regressionFindings.Count > 0)
            {
                sb.AppendLine("    <div class=\"callout callout-danger\">");
                sb.AppendLine($"      <div class=\"callout-title\">Performance Regressions Detected ({regressionFindings.Count})</div>");
                sb.AppendLine("      <p style=\"font-size: 0.9rem; margin-bottom: 0.5rem;\">The following queries regressed compared to the provided baseline:</p>");
                sb.AppendLine("      <ul style=\"font-size: 0.85rem; padding-left: 1.25rem;\">");
                foreach (var r in regressionFindings)
                {
                    sb.AppendLine($"        <li><a class=\"query-hash\" href=\"#query-{WebUtility.HtmlEncode(r.QueryHash)}\">{WebUtility.HtmlEncode(r.QueryHash)}</a>: {WebUtility.HtmlEncode(r.Description)}</li>");
                }
                sb.AppendLine("      </ul>");
                sb.AppendLine("    </div>");
            }

            if (nplusOneFindings.Count > 0)
            {
                sb.AppendLine("    <div class=\"callout\">");
                sb.AppendLine($"      <div class=\"callout-title\">N+1 Query Patterns Detected ({nplusOneFindings.Count})</div>");
                sb.AppendLine("      <p style=\"font-size: 0.9rem; margin-bottom: 0.5rem;\">Queries repeated in a single request:</p>");
                sb.AppendLine("      <ul style=\"font-size: 0.85rem; padding-left: 1.25rem;\">");
                foreach (var n in nplusOneFindings)
                {
                    sb.AppendLine($"        <li><a class=\"query-hash\" href=\"#query-{WebUtility.HtmlEncode(n.QueryHash)}\">{WebUtility.HtmlEncode(n.QueryHash)}</a>: {WebUtility.HtmlEncode(n.Description)}</li>");
                }
                sb.AppendLine("      </ul>");
                sb.AppendLine("    </div>");
            }

            if (explosionFindings.Count > 0)
            {
                sb.AppendLine("    <div class=\"callout callout-danger\">");
                sb.AppendLine($"      <div class=\"callout-title\">Query Explosion Detected ({explosionFindings.Count})</div>");
                sb.AppendLine("      <ul style=\"font-size: 0.85rem; padding-left: 1.25rem;\">");
                foreach (var exp in explosionFindings)
                {
                    sb.AppendLine($"        <li>{WebUtility.HtmlEncode(exp.Description)}</li>");
                }
                sb.AppendLine("      </ul>");
                sb.AppendLine("    </div>");
            }

            // Top Performance Rankings
            if (queries.Count > 0)
            {
                sb.AppendLine("    <div class=\"section-title\">Top Performance Culprits</div>");
                sb.AppendLine("    <div class=\"grid grid-3\">");

                // Top Slow
                sb.AppendLine("      <div class=\"card\">");
                sb.AppendLine("        <div class=\"card-title\">Slowest Queries (Avg Duration)</div>");
                sb.AppendLine("        <table>");
                sb.AppendLine("          <thead><tr><th>Hash</th><th class=\"text-right\">Avg ms</th><th class=\"text-right\">Max ms</th></tr></thead>");
                sb.AppendLine("          <tbody>");
                foreach (var q in topSlow)
                {
                    sb.AppendLine("            <tr>");
                    sb.AppendLine($"              <td><a class=\"query-hash\" href=\"#query-{WebUtility.HtmlEncode(q.QueryHash)}\">{WebUtility.HtmlEncode(q.QueryHash)}</a></td>");
                    sb.AppendLine($"              <td class=\"text-right\">{q.AverageDurationMs.ToString("N1", ci)}</td>");
                    sb.AppendLine($"              <td class=\"text-right\">{q.MaxDurationMs.ToString("N1", ci)}</td>");
                    sb.AppendLine("            </tr>");
                }
                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");
                sb.AppendLine("      </div>");

                // Top CPU
                sb.AppendLine("      <div class=\"card\">");
                sb.AppendLine("        <div class=\"card-title\">Highest CPU Time</div>");
                if (topCpu.Count == 0)
                {
                    sb.AppendLine("        <p style=\"font-size: 0.85rem; color: var(--text-muted); margin-top: 0.5rem;\">No CPU metrics available (requires Query Store).</p>");
                }
                else
                {
                    sb.AppendLine("        <table>");
                    sb.AppendLine("          <thead><tr><th>Hash</th><th class=\"text-right\">Avg CPU ms</th><th class=\"text-right\">Execs</th></tr></thead>");
                    sb.AppendLine("          <tbody>");
                    foreach (var q in topCpu)
                    {
                        sb.AppendLine("            <tr>");
                        sb.AppendLine($"              <td><a class=\"query-hash\" href=\"#query-{WebUtility.HtmlEncode(q.QueryHash)}\">{WebUtility.HtmlEncode(q.QueryHash)}</a></td>");
                        sb.AppendLine($"              <td class=\"text-right\">{q.AverageCpuMs.Value.ToString("N1", ci)}</td>");
                        sb.AppendLine($"              <td class=\"text-right\">{q.ExecutionCount.ToString("N0", ci)}</td>");
                        sb.AppendLine("            </tr>");
                    }
                    sb.AppendLine("          </tbody>");
                    sb.AppendLine("        </table>");
                }
                sb.AppendLine("      </div>");

                // Top Logical Reads
                sb.AppendLine("      <div class=\"card\">");
                sb.AppendLine("        <div class=\"card-title\">Highest Logical Reads</div>");
                if (topReads.Count == 0)
                {
                    sb.AppendLine("        <p style=\"font-size: 0.85rem; color: var(--text-muted); margin-top: 0.5rem;\">No I/O metrics available (requires Query Store).</p>");
                }
                else
                {
                    sb.AppendLine("        <table>");
                    sb.AppendLine("          <thead><tr><th>Hash</th><th class=\"text-right\">Avg Reads</th><th class=\"text-right\">Execs</th></tr></thead>");
                    sb.AppendLine("          <tbody>");
                    foreach (var q in topReads)
                    {
                        sb.AppendLine("            <tr>");
                        sb.AppendLine($"              <td><a class=\"query-hash\" href=\"#query-{WebUtility.HtmlEncode(q.QueryHash)}\">{WebUtility.HtmlEncode(q.QueryHash)}</a></td>");
                        sb.AppendLine($"              <td class=\"text-right\">{q.LogicalReads.Value.ToString("N0", ci)}</td>");
                        sb.AppendLine($"              <td class=\"text-right\">{q.ExecutionCount.ToString("N0", ci)}</td>");
                        sb.AppendLine("            </tr>");
                    }
                    sb.AppendLine("          </tbody>");
                    sb.AppendLine("        </table>");
                }
                sb.AppendLine("      </div>");

                sb.AppendLine("    </div>");
            }

            // All Findings Section
            sb.AppendLine("    <div class=\"section-title\">Findings &amp; Recommendations</div>");
            if (findings.Count == 0)
            {
                sb.AppendLine("    <div class=\"card\">");
                sb.AppendLine("      <p style=\"color: var(--color-good); font-size: 0.95rem;\">&check; No performance issues or rule violations were found in this run.</p>");
                sb.AppendLine("    </div>");
            }
            else
            {
                sb.AppendLine("    <div class=\"card\" style=\"overflow-x: auto;\">");
                sb.AppendLine("      <table>");
                sb.AppendLine("        <thead>");
                sb.AppendLine("          <tr>");
                sb.AppendLine("            <th>Severity</th>");
                sb.AppendLine("            <th>Rule ID</th>");
                sb.AppendLine("            <th>Confidence</th>");
                sb.AppendLine("            <th>Query</th>");
                sb.AppendLine("            <th>Details &amp; Recommendation</th>");
                sb.AppendLine("          </tr>");
                sb.AppendLine("        </thead>");
                sb.AppendLine("        <tbody>");
                foreach (var f in findings.OrderByDescending(f => string.Equals(f.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ? 3 : (string.Equals(f.Severity, "Warning", StringComparison.OrdinalIgnoreCase) ? 2 : 1)))
                {
                    var sev = (f.Severity ?? "Info").ToLowerInvariant();
                    var badgeClass = sev == "critical" ? "badge-critical" : (sev == "warning" ? "badge-warning" : "badge-info");
                    sb.AppendLine("          <tr>");
                    sb.AppendLine($"            <td><span class=\"badge {badgeClass}\">{WebUtility.HtmlEncode(f.Severity ?? "Info")}</span></td>");
                    sb.AppendLine($"            <td><code>{WebUtility.HtmlEncode(f.RuleId ?? string.Empty)}</code></td>");
                    sb.AppendLine($"            <td>{(f.Confidence * 100).ToString("0", ci)}%</td>");
                    sb.AppendLine("            <td>");
                    if (!string.IsNullOrWhiteSpace(f.QueryHash))
                    {
                        sb.AppendLine($"              <a class=\"query-hash\" href=\"#query-{WebUtility.HtmlEncode(f.QueryHash)}\">{WebUtility.HtmlEncode(f.QueryHash)}</a>");
                    }
                    else
                    {
                        sb.AppendLine("              <span style=\"color: var(--text-muted);\">Global</span>");
                    }
                    sb.AppendLine("            </td>");
                    sb.AppendLine("            <td>");
                    sb.AppendLine($"              <div style=\"font-weight: 600; margin-bottom: 0.25rem;\">{WebUtility.HtmlEncode(f.Title ?? string.Empty)}</div>");
                    sb.AppendLine($"              <div style=\"font-size: 0.85rem; color: var(--text-muted);\">{WebUtility.HtmlEncode(f.Description ?? string.Empty)}</div>");
                    if (!string.IsNullOrWhiteSpace(f.Recommendation))
                    {
                        sb.AppendLine($"              <div class=\"recommendation-box\">&rarr; {WebUtility.HtmlEncode(f.Recommendation)}</div>");
                    }
                    sb.AppendLine("            </td>");
                    sb.AppendLine("          </tr>");
                }
                sb.AppendLine("        </tbody>");
                sb.AppendLine("      </table>");
                sb.AppendLine("    </div>");
            }

            // Full Query Catalog
            sb.AppendLine("    <div class=\"section-title\">Query Catalog</div>");
            if (queries.Count == 0)
            {
                sb.AppendLine("    <div class=\"card\">");
                sb.AppendLine("      <p style=\"color: var(--text-muted); font-size: 0.9rem;\">No queries captured in this report.</p>");
                sb.AppendLine("    </div>");
            }
            else
            {
                foreach (var q in queries)
                {
                    var queryFindings = findings.Where(f => string.Equals(f.QueryHash, q.QueryHash, StringComparison.OrdinalIgnoreCase)).ToList();

                    sb.AppendLine($"    <div class=\"card\" id=\"query-{WebUtility.HtmlEncode(q.QueryHash)}\" style=\"margin-bottom: 1.5rem;\">");
                    sb.AppendLine("      <div style=\"display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; margin-bottom: 0.75rem;\">");
                    sb.AppendLine($"        <div><span class=\"query-hash\" style=\"font-size: 1.1rem;\">{WebUtility.HtmlEncode(q.QueryHash)}</span></div>");
                    sb.AppendLine("        <div style=\"font-size: 0.85rem; color: var(--text-muted);\">");
                    sb.AppendLine($"          Executions: <strong>{q.ExecutionCount.ToString("N0", ci)}</strong> | ");
                    sb.AppendLine($"          Avg: <strong>{q.AverageDurationMs.ToString("N1", ci)} ms</strong> | ");
                    sb.AppendLine($"          P95: <strong>{q.P95DurationMs.ToString("N1", ci)} ms</strong> | ");
                    sb.AppendLine($"          Max: <strong>{q.MaxDurationMs.ToString("N1", ci)} ms</strong>");
                    if (q.AverageCpuMs.HasValue)
                    {
                        sb.AppendLine($" | CPU: <strong>{q.AverageCpuMs.Value.ToString("N1", ci)} ms</strong>");
                    }
                    if (q.LogicalReads.HasValue)
                    {
                        sb.AppendLine($" | Reads: <strong>{q.LogicalReads.Value.ToString("N0", ci)}</strong>");
                    }
                    sb.AppendLine("        </div>");
                    sb.AppendLine("      </div>");

                    sb.AppendLine($"      <pre><code>{WebUtility.HtmlEncode(q.NormalizedSql ?? string.Empty)}</code></pre>");

                    if (queryFindings.Count > 0)
                    {
                        sb.AppendLine("      <div style=\"margin-top: 0.75rem; font-size: 0.85rem;\">");
                        sb.AppendLine("        <strong style=\"color: var(--text-muted);\">Findings for this query:</strong>");
                        sb.AppendLine("        <ul style=\"list-style: none; padding-left: 0; margin-top: 0.25rem;\">");
                        foreach (var qf in queryFindings)
                        {
                            var sev = (qf.Severity ?? "Info").ToLowerInvariant();
                            var badgeClass = sev == "critical" ? "badge-critical" : (sev == "warning" ? "badge-warning" : "badge-info");
                            sb.AppendLine($"          <li style=\"margin-bottom: 0.25rem;\"><span class=\"badge {badgeClass}\">{WebUtility.HtmlEncode(qf.Severity ?? "Info")}</span> <strong>{WebUtility.HtmlEncode(qf.RuleId)}</strong>: {WebUtility.HtmlEncode(qf.Title)} &mdash; <span style=\"color: var(--text-muted);\">{WebUtility.HtmlEncode(qf.Description)}</span></li>");
                        }
                        sb.AppendLine("        </ul>");
                        sb.AppendLine("      </div>");
                    }

                    sb.AppendLine("    </div>");
                }
            }

            // Footer
            sb.AppendLine("    <footer>");
            sb.AppendLine($"      Generated by <strong>QueryLens</strong> on {report.GeneratedUtc:yyyy-MM-dd HH:mm:ss} UTC. Single-file offline report.");
            sb.AppendLine("    </footer>");

            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
