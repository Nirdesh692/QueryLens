using System;
using System.Data.Common;
using System.Threading;
using QueryLens.AdoNet.Capture;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;

namespace QueryLens.AdoNet.Extensions
{
    public static class QueryLensAdoNet
    {
        private static readonly AsyncLocal<string> CurrentRequestId = new AsyncLocal<string>();

        public static QueryLensDbConnection Wrap(
            DbConnection connection,
            QueryLensPipeline pipeline,
            Func<string> requestIdProvider = null,
            string provider = "AdoNet",
            string orm = "AdoNet")
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            return new QueryLensDbConnection(connection, pipeline, requestIdProvider ?? GetCurrentRequestId, provider, orm);
        }

        public static (QueryLensPipeline Pipeline, QueryLensDbConnection Connection) CreatePipelineAndWrap(
            DbConnection connection,
            QueryLensOptions options = null,
            Func<string> requestIdProvider = null,
            string provider = "AdoNet",
            string orm = "AdoNet")
        {
            var pipeline = new QueryLensPipeline(options);
            var wrapped = Wrap(connection, pipeline, requestIdProvider, provider, orm);
            return (pipeline, wrapped);
        }

        public static void SetCurrentRequestId(string requestId) => CurrentRequestId.Value = requestId;

        public static string GetCurrentRequestId() => CurrentRequestId.Value;
    }
}
