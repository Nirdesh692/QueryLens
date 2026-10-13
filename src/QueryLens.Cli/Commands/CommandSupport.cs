using System;
using System.Globalization;
using System.IO;
using System.Text;
using QueryLens.Cli.Reporting;
using QueryLens.Core.Baselines;

namespace QueryLens.Cli.Commands
{
    internal static class CommandSupport
    {
        public static string RequireSingleInput(CliArguments args, string commandName)
        {
            if (args.Positionals.Count != 1)
            {
                throw new CliUsageException($"'{commandName}' expects exactly one input file.");
            }

            return args.Positionals[0];
        }

        public static BaselineSnapshot LoadBaseline(string path)
        {
            if (path == null)
            {
                return null;
            }

            var loaded = InputLoader.Load(path);
            return BaselineSnapshot.FromStatistics(loaded.Statistics, loaded.Label);
        }

        public static void WriteFile(string path, string content)
        {
            try
            {
                File.WriteAllText(path, content, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new CliUsageException($"Cannot write '{path}': {ex.Message}");
            }
        }

        public static double ParseNonNegativeNumber(string optionName, string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                throw new CliUsageException($"Option '--{optionName}' must be a non-negative number, got '{text}'.");
            }

            return value;
        }
    }
}
