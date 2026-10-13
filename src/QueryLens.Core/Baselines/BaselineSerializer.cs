using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QueryLens.Core.Baselines
{
    /// <summary>Reads and writes <see cref="BaselineSnapshot"/> as versioned, human-readable JSON.</summary>
    public static class BaselineSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize(BaselineSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonSerializer.Serialize(snapshot, Options);
        }

        public static BaselineSnapshot Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Baseline JSON is empty.", nameof(json));
            }

            BaselineSnapshot snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Baseline JSON is malformed: " + ex.Message, ex);
            }

            if (snapshot == null)
            {
                throw new InvalidDataException("Baseline JSON did not contain a snapshot.");
            }

            if (snapshot.SchemaVersion > BaselineSnapshot.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Baseline schema version {snapshot.SchemaVersion} is newer than the supported version {BaselineSnapshot.CurrentSchemaVersion}.");
            }

            if (snapshot.Queries == null)
            {
                snapshot.Queries = new System.Collections.Generic.List<Models.QueryStatistics>();
            }

            return snapshot;
        }

        public static void Save(BaselineSnapshot snapshot, string path)
        {
            File.WriteAllText(path, Serialize(snapshot), new UTF8Encoding(false));
        }

        public static BaselineSnapshot Load(string path)
        {
            return Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }
    }
}
