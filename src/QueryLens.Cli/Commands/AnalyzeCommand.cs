using System;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Cli.Analysis;
using QueryLens.Cli.Reporting;

namespace QueryLens.Cli.Commands
{
    /// <summary><c>analyze &lt;input&gt;</c>: runs rules and scoring, prints a summary, optionally saves the JSON report.</summary>
    public sealed class AnalyzeCommand : ICliCommand
    {
        private static readonly string[] Options = { "baseline", "output", "label" };

        public string Name => "analyze";

        public string Summary => "Run the rules and scorer over captured statistics and print a summary.";

        public string Usage => "analyze <input.json> [--baseline <baseline.json>] [--output <report.json>] [--label <text>]";

        public int Execute(IReadOnlyList<string> args, CliContext context)
        {
            var parsed = CliArguments.Parse(args, Options);
            var input = InputLoader.Load(CommandSupport.RequireSingleInput(parsed, Name));
            var baseline = CommandSupport.LoadBaseline(parsed.GetOption("baseline"));

            var report = AnalysisService.Analyze(input.Statistics, baseline);
            var document = ReportMapper.ToDocument(report, parsed.GetOption("label") ?? input.Label, context.UtcNow());

            var output = context.Output;
            output.WriteLine($"Performance score: {document.Score.Value}/100");
            output.WriteLine($"Queries: {document.Queries.Count}, executions: {document.Queries.Sum(q => q.ExecutionCount)}");
            output.WriteLine($"Findings: {document.Findings.Count}");
            foreach (var f in document.Findings)
            {
                output.WriteLine($"  [{f.Severity}] {f.RuleId} (confidence {f.Confidence:0.00}) {f.QueryHash} - {f.Title}");
            }

            var path = parsed.GetOption("output");
            if (path != null)
            {
                CommandSupport.WriteFile(path, ReportSerializer.Serialize(document));
                output.WriteLine($"Report written to {path}");
            }

            return ExitCodes.Success;
        }
    }
}
