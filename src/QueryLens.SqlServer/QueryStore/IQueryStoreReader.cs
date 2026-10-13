using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace QueryLens.SqlServer.QueryStore
{
    public interface IQueryStoreReader
    {
        /// <param name="connection">An already-open connection to the target SQL Server database.</param>
        /// <param name="options">Read options (since/top). Defaults are used if null.</param>
        /// <param name="cancellationToken">Cancellation token for the underlying command execution.</param>
        Task<IReadOnlyList<QueryStoreEntry>> ReadAsync(
            DbConnection connection,
            QueryStoreReadOptions options = null,
            CancellationToken cancellationToken = default);
    }
}
