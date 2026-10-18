using System;
using System.Collections.Generic;
using QueryLens.Cli.Reporting;
using Xunit;

namespace QueryLens.Cli.Tests
{
    public class HtmlReportRendererTests
    {
        private readonly HtmlReportRenderer _renderer = new HtmlReportRenderer();

        [Fact]
        public void Format_is_html()
        {
            Assert.Equal("html", _renderer.Format);
        }

        [Fact]
        public void Render_throws_on_null_argument()
        {
            Assert.Throws<ArgumentNullException>(() => _renderer.Render(null));
        }

        [Fact]
        public void Render_handles_empty_report()
        {
            var report = new ReportDocument
            {
                GeneratedUtc = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc),
                Score = new ScoreDocument { Value = 100 }
            };

            var html = _renderer.Render(report);

            Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
            Assert.EndsWith("</html>\r\n", html);
            Assert.Contains("100 / 100", html);
            Assert.Contains("score-good", html);
            Assert.Contains("No score deductions applied", html);
            Assert.Contains("No queries captured in this report", html);
            Assert.Contains("No performance issues or rule violations were found", html);
        }

        [Fact]
        public void Render_escapes_malicious_content_against_xss()
        {
            var report = new ReportDocument
            {
                Label = "<script>alert('xss-label')</script>",
                Score = new ScoreDocument { Value = 45 },
                Queries = new List<QueryDocument>
                {
                    new QueryDocument
                    {
                        QueryHash = "bad<hash>",
                        NormalizedSql = "SELECT * FROM Users WHERE Name = '<script>evil()</script>'",
                        ExecutionCount = 10,
                        AverageDurationMs = 1500
                    }
                },
                Findings = new List<FindingDocument>
                {
                    new FindingDocument
                    {
                        RuleId = "EVIL<001>",
                        Severity = "Critical",
                        Confidence = 0.95,
                        QueryHash = "bad<hash>",
                        Title = "<img src=x onerror=alert(1)>",
                        Description = "Dangerous <b>payload</b>",
                        Recommendation = "Sanitize <input> data"
                    }
                }
            };

            var html = _renderer.Render(report);

            Assert.DoesNotContain("<script>alert('xss-label')</script>", html);
            Assert.Contains("&lt;script&gt;alert(&#39;xss-label&#39;)&lt;/script&gt;", html);

            Assert.DoesNotContain("<script>evil()</script>", html);
            Assert.Contains("&lt;script&gt;evil()&lt;/script&gt;", html);

            Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
            Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);

            Assert.DoesNotContain("<b>payload</b>", html);
            Assert.Contains("&lt;b&gt;payload&lt;/b&gt;", html);

            Assert.Contains("score-bad", html);
            Assert.Contains("45 / 100", html);
        }

        [Fact]
        public void Render_displays_warning_score_state()
        {
            var report = new ReportDocument
            {
                Score = new ScoreDocument
                {
                    Value = 72,
                    Components = new List<ScoreComponentDocument>
                    {
                        new ScoreComponentDocument { Name = "Duration", Penalty = 18, Reason = "Slow query detected" },
                        new ScoreComponentDocument { Name = "Logical Reads", Penalty = 10, Reason = "High reads" }
                    }
                },
                Queries = new List<QueryDocument>
                {
                    new QueryDocument
                    {
                        QueryHash = "hash1",
                        NormalizedSql = "SELECT 1",
                        ExecutionCount = 50,
                        AverageDurationMs = 450,
                        P95DurationMs = 600,
                        MaxDurationMs = 800,
                        AverageCpuMs = 120.5,
                        LogicalReads = 15000
                    }
                },
                Findings = new List<FindingDocument>
                {
                    new FindingDocument
                    {
                        RuleId = "SLOWQUERY001",
                        Severity = "Warning",
                        Confidence = 0.8,
                        QueryHash = "hash1",
                        Title = "Slow Query",
                        Description = "Average duration 450ms",
                        Recommendation = "Add index"
                    }
                }
            };

            var html = _renderer.Render(report);

            Assert.Contains("score-warn", html);
            Assert.Contains("72 / 100", html);
            Assert.Contains("-18", html);
            Assert.Contains("-10", html);
            Assert.Contains("120.5 ms", html);
            Assert.Contains("15,000", html);
            Assert.Contains("Top Performance Culprits", html);
            Assert.Contains("Slowest Queries", html);
            Assert.Contains("Highest CPU Time", html);
            Assert.Contains("Highest Logical Reads", html);
        }

        [Fact]
        public void Render_displays_diagnostic_callouts_for_regressions_nplusone_and_explosions()
        {
            var report = new ReportDocument
            {
                Score = new ScoreDocument { Value = 30 },
                Queries = new List<QueryDocument>
                {
                    new QueryDocument { QueryHash = "q1", NormalizedSql = "SELECT * FROM Orders WHERE Id = ?", ExecutionCount = 20, AverageDurationMs = 300 }
                },
                Findings = new List<FindingDocument>
                {
                    new FindingDocument
                    {
                        RuleId = "QUERYREGRESSION001",
                        Severity = "Critical",
                        Confidence = 1.0,
                        QueryHash = "q1",
                        Title = "Query Regressed",
                        Description = "Duration grew by 150%",
                        Recommendation = "Investigate recent commits"
                    },
                    new FindingDocument
                    {
                        RuleId = "NPLUSONE001",
                        Severity = "Warning",
                        Confidence = 0.9,
                        QueryHash = "q1",
                        Title = "N+1 Pattern",
                        Description = "Query executed 20 times in a single request",
                        Recommendation = "Use eager loading"
                    },
                    new FindingDocument
                    {
                        RuleId = "QUERYEXPLOSION001",
                        Severity = "Critical",
                        Confidence = 1.0,
                        QueryHash = null,
                        Title = "Query Explosion",
                        Description = "Total queries in request exceeded limit (55 > 50)",
                        Recommendation = "Batch queries"
                    }
                }
            };

            var html = _renderer.Render(report);

            Assert.Contains("Performance Regressions Detected (1)", html);
            Assert.Contains("N+1 Query Patterns Detected (1)", html);
            Assert.Contains("Query Explosion Detected (1)", html);
            Assert.Contains("Global", html); // QueryHash is null for explosion
        }
    }
}
