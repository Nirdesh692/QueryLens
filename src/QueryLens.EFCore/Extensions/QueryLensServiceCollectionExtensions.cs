using System;
using Microsoft.Extensions.DependencyInjection;
using QueryLens.Core.Configuration;
using QueryLens.Core.Pipeline;
using QueryLens.EFCore.Interceptors;

namespace QueryLens.EFCore.Extensions
{
    /// <summary>
    /// DI registration for QueryLens. Call <see cref="AddQueryLens"/> once at
    /// startup, then wire the resulting interceptor into your DbContext via
    /// <see cref="QueryLensDbContextOptionsExtensions.AddQueryLensInterceptor"/>.
    /// </summary>
    public static class QueryLensServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a singleton <see cref="QueryLensOptions"/>, <see cref="QueryLensPipeline"/>,
        /// and <see cref="QueryLensEfCoreInterceptor"/>. The interceptor's request-id
        /// provider defaults to <see cref="QueryLensEfCore.GetCurrentRequestId"/>
        /// (an ambient <see cref="System.Threading.AsyncLocal{T}"/> value) unless
        /// you supply <paramref name="requestIdProvider"/>.
        /// </summary>
        public static IServiceCollection AddQueryLens(
            this IServiceCollection services,
            Action<QueryLensOptions> configureOptions = null,
            Func<string> requestIdProvider = null)
        {
            var options = new QueryLensOptions();
            configureOptions?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton(sp => new QueryLensPipeline(sp.GetRequiredService<QueryLensOptions>()));
            services.AddSingleton(sp => new QueryLensEfCoreInterceptor(
                sp.GetRequiredService<QueryLensPipeline>(),
                requestIdProvider ?? QueryLensEfCore.GetCurrentRequestId));

            return services;
        }
    }
}
