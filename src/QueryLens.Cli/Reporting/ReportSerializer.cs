using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QueryLens.Cli.Reporting
{
    /// <summary>Reads and writes <see cref="ReportDocument"/> as indented camelCase JSON.</summary>
    public static class ReportSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize(ReportDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            return JsonSerializer.Serialize(document, Options);
        }

        public static ReportDocument Deserialize(string json)
        {
            ReportDocument document;
            try
            {
                document = JsonSerializer.Deserialize<ReportDocument>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Report JSON is malformed: " + ex.Message, ex);
            }

            if (document == null)
            {
                throw new InvalidDataException("Report JSON did not contain a report.");
            }

            if (document.ReportSchemaVersion > ReportDocument.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Report schema version {document.ReportSchemaVersion} is newer than the supported version {ReportDocument.CurrentSchemaVersion}.");
            }

            return document;
        }
    }
}
