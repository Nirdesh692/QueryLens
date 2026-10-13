using System;
using System.Data.Entity.Infrastructure.Interception;
using System.Runtime.Remoting.Messaging;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;
using QueryLens.EF6.Interceptors;

namespace QueryLens.EF6.Extensions
{
    /// <summary>
    /// Entry point for wiring QueryLens into an EF6 application. Call
    /// <see cref="Register(QueryLensOptions, Func{string})"/> once at startup
    /// (e.g. Global.asax's Application_Start) - no changes to individual LINQ
    /// queries or DbContexts are required.
    /// </summary>
    public static class QueryLensEf6
    {
        private const string RequestIdSlotName = "QueryLens.RequestId";

        /// <summary>
        /// Creates a new pipeline, registers the EF6 interceptor against it, and
        /// returns the pipeline so you can call <see cref="QueryLensPipeline.Analyze"/>
        /// later. If you don't supply a requestIdProvider, the default reads an
        /// ambient value set via <see cref="SetCurrentRequestId"/>.
        /// </summary>
        public static QueryLensPipeline Register(QueryLensOptions options = null, Func<string> requestIdProvider = null)
        {
            var pipeline = new QueryLensPipeline(options);
            Register(pipeline, requestIdProvider);
            return pipeline;
        }

        /// <summary>
        /// Registers the EF6 interceptor against an existing pipeline - useful if
        /// you're also feeding it from another adapter (e.g. a Dapper wrapper) and
        /// want everything aggregated together.
        /// </summary>
        public static void Register(QueryLensPipeline pipeline, Func<string> requestIdProvider = null)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            var interceptor = new QueryLensEf6Interceptor(pipeline, requestIdProvider ?? GetCurrentRequestId);
            DbInterception.Add(interceptor);
        }

        /// <summary>
        /// Sets the ambient request id for the current logical call context, so it
        /// flows across the async calls EF6 makes while running a query. Call this
        /// once per unit of work (e.g. an HttpModule's BeginRequest, or a message
        /// processing loop) if you rely on the default request-id provider instead
        /// of supplying your own to <see cref="Register(QueryLensOptions, Func{string})"/>.
        /// </summary>
        public static void SetCurrentRequestId(string requestId) =>
            CallContext.LogicalSetData(RequestIdSlotName, requestId);

        /// <summary>Reads the ambient request id set by <see cref="SetCurrentRequestId"/>.</summary>
        public static string GetCurrentRequestId() =>
            CallContext.LogicalGetData(RequestIdSlotName) as string;
    }
}
