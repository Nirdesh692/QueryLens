using System.Collections.Generic;
using QueryLens.Cli.Comparison;
using QueryLens.Cli.Reporting;
using QueryLens.Core.Baselines;

namespace QueryLens.Cli.Commands
{
    /// <summary><c>compare &lt;baseline&gt; &lt;current&gt;</c>: prints per-query changes. Informational; always exit 0 on success.</summary>
    public sealed class CompareCommand : ICliCommand
    {
        public string Name => "compare";

        public string Summary => "Compare a baseline against a current run, query by query.";

        public string Usage => "compare <baseline.json> <current.json>";

        public int Execute(IReadOnlyList<string> args, CliContext context)
        {
            var parsed = CliArguments.Parse(args, new string[0]);
            if (parsed.Positionals.Count != 2)
            {
                throw new CliUsageException("'compare' expects <baseline.json> and <current.json>.");
            }

            var baselineInput = InputLoader.Load(parsed.Positionals[0]);
            var currentInput = InputLoader.Load(parsed.Positionals[1]);
            var baseline = BaselineSnapshot.FromStatistics(baselineInput.Statistics, baselineInput.Label);

            var comparison = new BaselineComparer().Compare(baseline, currentInput.Statistics);
            context.Output.Write(CompareFormatter.Format(comparison));
            return ExitCodes.Success;
        }
    }
}
