using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QueryLens.EFCore.Interceptors;

namespace QueryLens.EFCore.Extensions
{
    public static class QueryLensDbContextOptionsExtensions
    {
        /// <summary>
        /// Adds the QueryLens interceptor registered via
        /// <see cref="QueryLensServiceCollectionExtensions.AddQueryLens"/> to this
        /// DbContext. Use inside the (sp, options) overload of AddDbContext:
        /// <code>
        /// services.AddDbContext&lt;MyDbContext&gt;((sp, options) =>
        ///     options.UseSqlServer(connectionString).AddQueryLensInterceptor(sp));
        /// </code>
        /// </summary>
        public static DbContextOptionsBuilder AddQueryLensInterceptor(
            this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
        {
            if (optionsBuilder == null)
            {
                throw new ArgumentNullException(nameof(optionsBuilder));
            }

            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var interceptor = serviceProvider.GetRequiredService<QueryLensEfCoreInterceptor>();
            return optionsBuilder.AddInterceptors(interceptor);
        }
    }
}
