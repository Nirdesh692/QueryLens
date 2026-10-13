using System;

namespace QueryLens.SqlServer.QueryStore
{
    public sealed class QueryStoreReadOptions
    {
        /// <summary>Only include runtime stats intervals starting at or after this time. Null reads all retained history.</summary>
        public DateTimeOffset? SinceUtc { get; set; }

        /// <summary>Maximum number of distinct queries to return, ordered by execution count descending.</summary>
        public int Top { get; set; } = 500;
    }

    /// <summary>Thrown when Query Store is not enabled on the target database.</summary>
    public sealed class QueryStoreNotEnabledException : Exception
    {
        public QueryStoreNotEnabledException()
            : base("Query Store is not enabled on this database. Enable it with ALTER DATABASE ... SET QUERY_STORE = ON, or point QueryLens at a database where it's already on.")
        {
        }

        public QueryStoreNotEnabledException(string message) : base(message)
        {
        }
    }
}
