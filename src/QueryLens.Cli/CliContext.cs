using System;
using System.IO;
using QueryLens.Cli.Reporting;

namespace QueryLens.Cli
{
    /// <summary>Everything a command needs from its host, so commands never touch Console or the clock directly.</summary>
    public sealed class CliContext
    {
        public CliContext(TextWriter output, TextWriter error, ReportRendererRegistry renderers, Func<DateTime> utcNow = null)
        {
            Output = output;
            Error = error;
            Renderers = renderers;
            UtcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        public ReportRendererRegistry Renderers { get; }

        public Func<DateTime> UtcNow { get; }
    }
}
