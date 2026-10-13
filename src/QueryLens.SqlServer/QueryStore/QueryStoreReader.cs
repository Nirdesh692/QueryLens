using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace QueryLens.SqlServer.QueryStore
{
    /// <summary>
    /// Reads aggregated query performance data from SQL Server's Query Store
    /// (sys.query_store_* catalog views, available since SQL Server 2016).
    ///
    /// This class is deliberately split into two halves with very different
    /// confidence levels: the T-SQL text (<see cref="EnabledCheckSql"/>,
    /// <see cref="SelectSql"/>) can only be proven correct against a real SQL
    /// Server instance, which this delivery had no access to. The row-mapping
    /// logic (<see cref="MapRow"/>) takes whatever an IDataRecord hands it and
    /// has no SQL Server dependency at all, so it's independently unit tested
    /// against a fake reader with controlled column data.
    /// </summary>
    public sealed class QueryStoreReader : IQueryStoreReader
    {
        internal const string EnabledCheckSql = "SELECT actual_state FROM sys.database_query_store_options;";

        // Aggregates runtime stats across all retained intervals with a
        // weighted average (SUM(avg*count)/SUM(count)) rather than a naive
        // AVG() of per-interval averages, since intervals can carry very
        // different execution counts and a naive average would misweight them.
        // Query Store reports duration/CPU in microseconds; those are aliased
        // with a "Us" suffix here and converted to milliseconds in MapRow.
        internal const string SelectSql = @"
SELECT TOP (@Top)
    q.query_id AS QueryId,
    p.plan_id AS PlanId,
    qt.query_sql_text AS QueryText,
    SUM(rs.count_executions) AS ExecutionCount,
    CASE WHEN SUM(rs.count_executions) > 0
         THEN SUM(rs.avg_duration * rs.count_executions) / SUM(rs.count_executions)
         ELSE 0 END AS AvgDurationUs,
    CASE WHEN SUM(rs.count_executions) > 0
         THEN SUM(rs.avg_cpu_time * rs.count_executions) / SUM(rs.count_executions)
         ELSE 0 END AS AvgCpuUs,
    CASE WHEN SUM(rs.count_executions) > 0
         THEN SUM(rs.avg_logical_io_reads * rs.count_executions) / SUM(rs.count_executions)
         ELSE 0 END AS AvgLogicalReads,
    CASE WHEN SUM(rs.count_executions) > 0
         THEN SUM(rs.avg_physical_io_reads * rs.count_executions) / SUM(rs.count_executions)
         ELSE 0 END AS AvgPhysicalReads,
    CASE WHEN SUM(rs.count_executions) > 0
         THEN SUM(rs.avg_query_max_used_memory * rs.count_executions) / SUM(rs.count_executions)
         ELSE NULL END AS AvgMemoryKb,
    MAX(rs.last_execution_time) AS LastExecutionTime,
    CONVERT(NVARCHAR(MAX), p.query_plan) AS ExecutionPlanXml
FROM sys.query_store_query q
INNER JOIN sys.query_store_query_text qt ON q.query_text_id = qt.query_text_id
INNER JOIN sys.query_store_plan p ON p.query_id = q.query_id
INNER JOIN sys.query_store_runtime_stats rs ON rs.plan_id = p.plan_id
INNER JOIN sys.query_store_runtime_stats_interval rsi ON rs.runtime_stats_interval_id = rsi.runtime_stats_interval_id
WHERE (@SinceUtc IS NULL OR rsi.start_time >= @SinceUtc)
  AND q.is_internal_query = 0
GROUP BY q.query_id, p.plan_id, qt.query_sql_text, p.query_plan
ORDER BY ExecutionCount DESC;";

        public async Task<IReadOnlyList<QueryStoreEntry>> ReadAsync(
            DbConnection connection,
            QueryStoreReadOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            options = options ?? new QueryStoreReadOptions();

            await EnsureEnabledAsync(connection, cancellationToken).ConfigureAwait(false);

            var entries = new List<QueryStoreEntry>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = SelectSql;
                AddParameter(command, "@Top", options.Top);
                AddParameter(command, "@SinceUtc", (object)options.SinceUtc ?? DBNull.Value);

                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        entries.Add(MapRow(reader));
                    }
                }
            }

            return entries;
        }

        private static async Task EnsureEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = EnabledCheckSql;

                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                // actual_state: 0 = Off. Any other value (1 = Read Write, 2 =
                // Read Only, etc.) means Query Store is on in some capacity.
                if (result == null || result == DBNull.Value || Convert.ToInt32(result) == 0)
                {
                    throw new QueryStoreNotEnabledException();
                }
            }
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// Maps one result row to a <see cref="QueryStoreEntry"/>. Takes the
        /// narrower <see cref="IDataRecord"/> rather than DbDataReader
        /// specifically so it can be unit tested against a lightweight fake
        /// without needing a full reader implementation.
        /// </summary>
        internal static QueryStoreEntry MapRow(IDataRecord record)
        {
            return new QueryStoreEntry
            {
                QueryId = record.GetInt64(record.GetOrdinal("QueryId")),
                PlanId = record.GetInt64(record.GetOrdinal("PlanId")),
                QueryText = record.GetString(record.GetOrdinal("QueryText")),
                ExecutionCount = record.GetInt64(record.GetOrdinal("ExecutionCount")),
                AvgDurationMs = MicrosecondsToMilliseconds(record.GetDouble(record.GetOrdinal("AvgDurationUs"))),
                AvgCpuMs = MicrosecondsToMilliseconds(record.GetDouble(record.GetOrdinal("AvgCpuUs"))),
                AvgLogicalReads = record.GetDouble(record.GetOrdinal("AvgLogicalReads")),
                AvgPhysicalReads = record.GetDouble(record.GetOrdinal("AvgPhysicalReads")),
                AvgMemoryGrantKb = GetNullableDouble(record, "AvgMemoryKb"),
                LastExecutionTimeUtc = GetNullableDateTime(record, "LastExecutionTime"),
                ExecutionPlanXml = GetNullableString(record, "ExecutionPlanXml")
            };
        }

        private static double MicrosecondsToMilliseconds(double microseconds) => microseconds / 1000.0;

        private static double? GetNullableDouble(IDataRecord record, string column)
        {
            var ordinal = record.GetOrdinal(column);
            return record.IsDBNull(ordinal) ? (double?)null : record.GetDouble(ordinal);
        }

        private static DateTime? GetNullableDateTime(IDataRecord record, string column)
        {
            var ordinal = record.GetOrdinal(column);
            return record.IsDBNull(ordinal) ? (DateTime?)null : record.GetDateTime(ordinal);
        }

        private static string GetNullableString(IDataRecord record, string column)
        {
            var ordinal = record.GetOrdinal(column);
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }
    }
}
