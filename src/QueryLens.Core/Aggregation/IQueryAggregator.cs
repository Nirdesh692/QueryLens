using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Aggregation
{
    public interface IQueryAggregator
    {
        void Add(QueryExecution execution);

        /// <summary>Returns null if the query hash has not been observed.</summary>
        QueryStatistics GetStatistics(string queryHash);

        IReadOnlyCollection<QueryStatistics> GetAllStatistics();

        void Reset();
    }
}
