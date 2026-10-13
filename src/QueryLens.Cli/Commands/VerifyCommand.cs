using System.Collections.Generic;
using QueryLens.Cli.Reporting;
using QueryLens.Cli.Verification;
using QueryLens.Core.Baselines;
using QueryLens.Core.Configuration;

namespace QueryLens.Cli.Commands
{
    /// <summary>
    /// <c>verify</c>: CI gate. Exit 0 = pass, 1 = a limit was exceeded, 2 = bad usage or input.
    /// <c>--max-query-count</c> limits the total number of query executions in the current run.
    /// </summary>
    public sealed class VerifyCommand : ICliCommand
    {
        private static readonly string[] Options = { "baseline", "max-duration-regression", "max-query-count" };

        public string Name => "verify";

        public string Summary => "Fail (non-zero exit) when limits are exceeded; for CI gates.";

        public string Usage => "verify <current.json> [--baseline <baseline.json> --max-duration-regression <percent>] [--max-query-count <n>]";

        public int Execute(IReadOnlyList<string> args, CliContext context)
        {
            var parsed = CliArguments.Parse(args, Options);
            var currentPath = CommandSupport.RequireSingleInput(parsed, Name);

            double? maxRegression = null;
            long? maxCount = null;

            if (parsed.TryGetOption("max-duration-regression", out var regressionText))
            {
                maxRegression = CommandSupport.ParseNonNegativeNumber("max-duration-regression", regressionText);
            }

            if (parsed.TryGetOption("max-query-count", out var countText))
            {
                var count = CommandSupport.ParseNonNegativeNumber("max-query-count", countText);
                if (count != System.Math.Floor(count))
                {
                    throw new CliUsageException($"Option '--max-query-count' must be a whole number, got '{countText}'.");
                }

                maxCount = (long)count;
            }

            if (!maxRegression.HasValue && !maxCount.HasValue)
            {
                throw new CliUsageException("'verify' needs at least one of --max-duration-regression or --max-query-count.");
            }

            var baselinePath = parsed.GetOption("baseline");
            if (maxRegression.HasValue && baselinePath == null)
            {
                throw new CliUsageException("--max-duration-regression requires --baseline <baseline.json>.");
            }

            var current = InputLoader.Load(currentPath);
            BaselineComparison comparison = null;
            if (baselinePath != null)
            {
                var baseline = CommandSupport.LoadBaseline(baselinePath);
                comparison = new BaselineComparer().Compare(baseline, current.Statistics);
            }

            var result = VerifyEvaluator.Evaluate(
                current.Statistics,
                comparison,
                maxRegression,
                maxCount,
                new QueryLensOptions().Thresholds.RegressionMinBaselineMs);

            if (result.Passed)
            {
                context.Output.WriteLine("verify: PASS");
                return ExitCodes.Success;
            }

            context.Output.WriteLine("verify: FAIL");
            foreach (var violation in result.Violations)
            {
                context.Output.WriteLine("  " + violation);
            }

            return ExitCodes.VerificationFailed;
        }
    }
}
