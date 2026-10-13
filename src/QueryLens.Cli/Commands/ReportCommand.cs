using System;
using System.Collections.Generic;
using QueryLens.Cli.Analysis;
using QueryLens.Cli.Reporting;

namespace QueryLens.Cli.Commands
{
    /// <summary>
    /// <c>report &lt;input&gt; --format json|html</c>. Input may be a saved report (re-emitted as is)
    /// or a statistics snapshot (analyzed first). Formats are resolved through
    /// <see cref="ReportRendererRegistry"/>; the Phase 9 HTML renderer registers there.
    /// </summary>
    public sealed class ReportCommand : ICliCommand
    {
        private static readonly string[] Options = { "format", "output", "baseline", "label" };

        public string Name => "report";

        public string Summary => "Render a report from a saved report or statistics file.";

        public string Usage => "report <input.json> [--format json|html] [--output <file>] [--baseline <baseline.json>] [--label <text>]";

        public int Execute(IReadOnlyList<string> args, CliContext context)
        {
            var parsed = CliArguments.Parse(args, Options);
            var format = parsed.GetOption("format") ?? "json";

            if (!context.Renderers.TryGet(format, out var renderer))
            {
                var reason = context.Renderers.IsPlanned(format)
                    ? $"Report format '{format}' is not available yet."
                    : $"Unknown report format '{format}'.";
                throw new CliUsageException(reason);
            }

            var input = InputLoader.Load(CommandSupport.RequireSingleInput(parsed, Name));

            ReportDocument document;
            if (input.Report != null && parsed.GetOption("baseline") == null)
            {
                document = input.Report;
                if (parsed.GetOption("label") != null)
                {
                    document.Label = parsed.GetOption("label");
                }
            }
            else
            {
                var baseline = CommandSupport.LoadBaseline(parsed.GetOption("baseline"));
                var report = AnalysisService.Analyze(input.Statistics, baseline);
                document = ReportMapper.ToDocument(report, parsed.GetOption("label") ?? input.Label, context.UtcNow());
            }

            var rendered = renderer.Render(document);
            var path = parsed.GetOption("output");
            if (path != null)
            {
                CommandSupport.WriteFile(path, rendered);
            }
            else
            {
                context.Output.WriteLine(rendered);
            }

            return ExitCodes.Success;
        }
    }
}
