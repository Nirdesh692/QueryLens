using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QueryLens.Cli.Commands;
using QueryLens.Cli.Reporting;

namespace QueryLens.Cli
{
    /// <summary>Testable entry point: dispatches to commands and maps failures to exit codes. <c>Main</c> only calls this.</summary>
    public sealed class CliApp
    {
        private readonly IReadOnlyList<ICliCommand> _commands;
        private readonly ReportRendererRegistry _renderers;
        private readonly Func<DateTime> _utcNow;

        public CliApp(ReportRendererRegistry renderers = null, Func<DateTime> utcNow = null)
        {
            _renderers = renderers ?? ReportRendererRegistry.CreateDefault();
            _utcNow = utcNow;
            _commands = new ICliCommand[]
            {
                new AnalyzeCommand(),
                new ReportCommand(),
                new CompareCommand(),
                new VerifyCommand()
            };
        }

        public int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
        {
            if (args.Count == 0 || IsHelp(args[0]))
            {
                WriteHelp(args.Count == 0 ? error : output);
                return args.Count == 0 ? ExitCodes.UsageError : ExitCodes.Success;
            }

            var command = _commands.FirstOrDefault(c => string.Equals(c.Name, args[0], StringComparison.OrdinalIgnoreCase));
            if (command == null)
            {
                error.WriteLine($"Unknown command '{args[0]}'.");
                WriteHelp(error);
                return ExitCodes.UsageError;
            }

            var context = new CliContext(output, error, _renderers, _utcNow);
            try
            {
                return command.Execute(args.Skip(1).ToList(), context);
            }
            catch (CliUsageException ex)
            {
                error.WriteLine("Error: " + ex.Message);
                error.WriteLine("Usage: querylens " + command.Usage);
                return ExitCodes.UsageError;
            }
        }

        private void WriteHelp(TextWriter writer)
        {
            writer.WriteLine("querylens - database query performance analysis");
            writer.WriteLine();
            writer.WriteLine("Commands:");
            foreach (var c in _commands)
            {
                writer.WriteLine($"  {c.Usage}");
                writer.WriteLine($"      {c.Summary}");
            }

            writer.WriteLine();
            writer.WriteLine("Exit codes: 0 = success/pass, 1 = verification failed, 2 = usage or input error.");
        }

        private static bool IsHelp(string arg) =>
            arg == "--help" || arg == "-h" || arg == "help" || arg == "-?";
    }
}
