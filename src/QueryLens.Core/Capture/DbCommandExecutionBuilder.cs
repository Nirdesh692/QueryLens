using System;
using System.Collections.Generic;
using System.Data.Common;
using QueryLens.Core.Models;

namespace QueryLens.Core.Capture
{
    /// <summary>
    /// Builds a <see cref="QueryExecution"/> from raw ADO.NET command data
    /// (<see cref="DbCommand"/>, from System.Data.Common - part of the BCL).
    /// This has no dependency on any specific ORM's interception types, so it
    /// is shared by the EF6 and EF Core adapters, and is the natural building
    /// block for a future plain-ADO.NET/Dapper capture path.
    /// </summary>
    public static class DbCommandExecutionBuilder
    {
        /// <param name="command">The executed command. Only its text, type, connection, and parameters are read.</param>
        /// <param name="duration">Elapsed execution time, however the caller measured it.</param>
        /// <param name="requestId">Correlates this execution with others in the same unit of work, or null.</param>
        /// <param name="captureParameters">Whether to populate <see cref="QueryExecution.Parameters"/> (name/type/size only, never values).</param>
        /// <param name="provider">Capture source tag, e.g. "EF6", "EFCore", "AdoNet".</param>
        /// <param name="orm">ORM tag, e.g. "EF6", "EFCore", "Dapper". Pass the same value as <paramref name="provider"/> when there is no separate ORM layer.</param>
        /// <param name="framework">.NET runtime/framework tag, e.g. ".NETFramework" or ".NET". Optional.</param>
        public static QueryExecution Build(
            DbCommand command,
            TimeSpan duration,
            string requestId,
            bool captureParameters,
            string provider,
            string orm,
            string framework = null)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return new QueryExecution
            {
                Sql = command.CommandText,
                Duration = duration,
                CommandType = command.CommandType.ToString(),
                Database = TryGet(() => command.Connection?.Database),
                Server = TryGet(() => command.Connection?.DataSource),
                RequestId = requestId,
                Provider = provider,
                Orm = orm,
                Framework = framework,
                Parameters = captureParameters ? BuildParameters(command.Parameters) : null
            };
        }

        private static IReadOnlyList<QueryParameter> BuildParameters(DbParameterCollection parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return Array.Empty<QueryParameter>();
            }

            var result = new List<QueryParameter>(parameters.Count);
            foreach (DbParameter parameter in parameters)
            {
                result.Add(new QueryParameter
                {
                    Name = parameter.ParameterName,
                    DbType = parameter.DbType.ToString(),
                    Size = parameter.Size
                });
            }

            return result;
        }

        private static string TryGet(Func<string> accessor)
        {
            try
            {
                return accessor();
            }
            catch
            {
                // Some providers throw if accessed in the wrong connection state;
                // never let telemetry collection break the caller's query.
                return null;
            }
        }
    }
}
