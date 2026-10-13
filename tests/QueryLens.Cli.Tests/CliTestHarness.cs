using System;
using System.IO;
using QueryLens.Core.Baselines;
using QueryLens.Core.Models;

namespace QueryLens.Cli.Tests
{
    /// <summary>Temp-directory fixture plus helpers to write inputs and run the CLI in-process.</summary>
    public sealed class CliTestHarness : IDisposable
    {
        public static readonly DateTime FixedNow = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "querylens-cli-" + Guid.NewGuid().ToString("N"));

        public CliTestHarness()
        {
            Directory.CreateDirectory(_dir);
        }

        public string PathFor(string name) => Path.Combine(_dir, name);

        public static QueryStatistics Stat(string hash, double avgMs, long count = 10, double? reads = null) =>
            new QueryStatistics
            {
                QueryHash = hash,
                NormalizedSql = "SELECT * FROM T_" + hash + " WHERE Id = ?",
                ExecutionCount = count,
                AverageDurationMs = avgMs,
                P95DurationMs = avgMs * 1.5,
                MaxDurationMs = avgMs * 2,
                AverageLogicalReads = reads
            };

        public string WriteSnapshot(string name, params QueryStatistics[] stats)
        {
            var path = PathFor(name);
            BaselineSerializer.Save(BaselineSnapshot.FromStatistics(stats, name), path);
            return path;
        }

        public CliResult Run(params string[] args)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var code = new CliApp(utcNow: () => FixedNow).Run(args, output, error);
            return new CliResult(code, output.ToString(), error.ToString());
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    public sealed class CliResult
    {
        public CliResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }
    }
}
