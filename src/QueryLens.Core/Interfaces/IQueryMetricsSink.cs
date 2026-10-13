using System.Threading;
using System.Threading.Tasks;
using QueryLens.Core.Models;

namespace QueryLens.Core.Interfaces
{
    /// <summary>
    /// Destination for captured query executions (file, database, OpenTelemetry,
    /// console, etc). Implementations should avoid blocking the application's
    /// database-command path - buffer and flush in the background rather than
    /// performing synchronous I/O directly inside <see cref="WriteAsync"/>
    /// where practical.
    /// </summary>
    public interface IQueryMetricsSink
    {
        Task WriteAsync(QueryExecution execution, CancellationToken cancellationToken = default);
    }
}
