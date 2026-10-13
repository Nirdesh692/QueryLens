using System;
using System.Threading;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;
using QueryLens.EFCore.Interceptors;

namespace QueryLens.EFCore.Extensions
{
    /// <summary>
    /// Entry point for wiring QueryLens into an EF Core application without DI
    /// (console apps, background workers). If you're using
    /// Microsoft.Extensions.DependencyInjection, prefer
    /// <see cref="QueryLensServiceCollectionExtensions.AddQueryLens"/> instead.
    /// </summary>
    public static class QueryLensEfCore
    {
        // AsyncLocal is the modern-.NET equivalent of the CallContext mechanism
        // the EF6 adapter uses: it flows across await boundaries within a
        // logical call chain, without requiring any ASP.NET Core dependency here.
        private static readonly AsyncLocal<string> CurrentRequestId = new AsyncLocal<string>();

        /// <summary>Creates a new pipeline and a ready-to-register interceptor for it.</summary>
        public static (QueryLensPipeline Pipeline, QueryLensEfCoreInterceptor Interceptor) CreatePipelineAndInterceptor(
            QueryLensOptions options = null, Func<string> requestIdProvider = null)
        {
            var pipeline = new QueryLensPipeline(options);
            var interceptor = CreateInterceptor(pipeline, requestIdProvider);
            return (pipeline, interceptor);
        }

        /// <summary>Creates an interceptor for an existing pipeline - use with <c>optionsBuilder.AddInterceptors(...)</c> directly.</summary>
        public static QueryLensEfCoreInterceptor CreateInterceptor(QueryLensPipeline pipeline, Func<string> requestIdProvider = null) =>
            new QueryLensEfCoreInterceptor(pipeline, requestIdProvider ?? GetCurrentRequestId);

        /// <summary>
        /// Sets the ambient request id for the current async flow (e.g. from
        /// ASP.NET Core middleware, using <c>HttpContext.TraceIdentifier</c>).
        /// Call this once per unit of work if you rely on the default
        /// request-id provider instead of supplying your own.
        /// </summary>
        public static void SetCurrentRequestId(string requestId) => CurrentRequestId.Value = requestId;

        /// <summary>Reads the ambient request id set by <see cref="SetCurrentRequestId"/>.</summary>
        public static string GetCurrentRequestId() => CurrentRequestId.Value;
    }
}
