using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using QueryLens.Core.Baselines;
using QueryLens.Core.Models;

namespace QueryLens.Cli.Reporting
{
    /// <summary>Statistics read from an input file, plus the original report when the file was one.</summary>
    public sealed class LoadedInput
    {
        public string Label { get; set; }

        public List<QueryStatistics> Statistics { get; set; }

        /// <summary>Non-null when the file was a CLI report (as opposed to a baseline snapshot).</summary>
        public ReportDocument Report { get; set; }
    }

    /// <summary>Loads either a CLI report JSON or a Core baseline snapshot JSON.</summary>
    public static class InputLoader
    {
        public static LoadedInput Load(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new CliUsageException($"Cannot read '{path}': {ex.Message}");
            }

            return Parse(json, path);
        }

        public static LoadedInput Parse(string json, string source)
        {
            try
            {
                if (IsReport(json))
                {
                    var report = ReportSerializer.Deserialize(json);
                    return new LoadedInput { Label = report.Label, Statistics = ReportMapper.ToStatistics(report), Report = report };
                }

                var snapshot = BaselineSerializer.Deserialize(json);
                return new LoadedInput { Label = snapshot.Label, Statistics = snapshot.Queries };
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException)
            {
                throw new CliUsageException($"Invalid input '{source}': {ex.Message}");
            }
        }

        private static bool IsReport(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    return doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("reportSchemaVersion", out _);
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
