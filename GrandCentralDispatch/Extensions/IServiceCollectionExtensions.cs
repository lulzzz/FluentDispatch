using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Metrics;
using GrandCentralDispatch.Options;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Extensions
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Configure <see cref="ICluster{TInput1}"/>
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        public static void ConfigureCluster(this IServiceCollection services,
            Action<ClusterOptions> clusterOptions,
            Action<CircuitBreakerOptions> circuitBreakerOptions)
        {
            services.Configure<ClusterOptions>(clusterOptions);
            services.Configure<CircuitBreakerOptions>(circuitBreakerOptions);
        }

        /// <summary>
        /// Add <see cref="IAsyncCluster{TInput,TOutput}"/> to <see cref="IServiceCollection"/>
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput"></typeparam>
        /// <param name="resolver"><see cref="Resolver{T}"/></param>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        public static void AddAsyncCluster<TInput, TOutput>(this IServiceCollection services,
                        Func<IServiceProvider, RemoteResolver<TInput, TOutput>> resolver)
        {
            services.TryAddSingleton(resolver);
            services.TryAddSingleton<IAsyncCluster<TInput, TOutput>, AsyncCluster<TInput, TOutput>>();
            services.TryAddSingleton<IExposeMetrics>(sp => sp.GetRequiredService<IAsyncCluster<TInput, TOutput>>());
        }

        /// <summary>
        /// Add <see cref="ICluster{TInput1}"/> to <see cref="IServiceCollection"/>
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="resolver"><see cref="Resolver{TInput1}"/></param>
        public static void AddCluster<TInput>(this IServiceCollection services,
            Func<IServiceProvider, Resolver<TInput>> resolver)
        {
            services.TryAddSingleton(resolver);
            services.TryAddSingleton<ICluster<TInput>, Cluster<TInput>>();
            services.TryAddSingleton<IExposeMetrics>(sp => sp.GetRequiredService<ICluster<TInput>>());
        }

        /// <summary>
        /// Add <see cref="ICluster{TInput1,TInput2}"/> to <see cref="IServiceCollection"/>
        /// </summary>
        /// <typeparam name="TInput1"></typeparam>
        /// <typeparam name="TInput2"></typeparam>
        /// <typeparam name="TOutput1"></typeparam>
        /// <typeparam name="TOutput2"></typeparam>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="item1Resolver"><see cref="Resolver{TInput1}"/></param>
        /// <param name="item2Resolver"><see cref="Resolver{TInput2}"/></param>
        /// <param name="finalResolver"><see cref="Resolver{TOutput1}"/></param>
        public static void AddCluster<TInput1, TInput2, TOutput1, TOutput2>(this IServiceCollection services,
            Func<IServiceProvider, PartialResolver<TInput1, TOutput1>> item1Resolver,
            Func<IServiceProvider, PartialResolver<TInput2, TOutput2>> item2Resolver,
            Func<IServiceProvider, DualResolver<TOutput1, TOutput2>> finalResolver)
        {
            services.AddMemoryCache();
            services.TryAddSingleton(item1Resolver);
            services.TryAddSingleton(item2Resolver);
            services.TryAddSingleton(finalResolver);
            services.TryAddSingleton<ICluster<TInput1, TInput2>, Cluster<TInput1, TInput2, TOutput1, TOutput2>>();
            services.TryAddSingleton<IExposeMetrics>(sp => sp.GetRequiredService<ICluster<TInput1, TInput2>>());
        }

        /// <summary>
        /// Add <see cref="ICluster{TInput1,TInput2}"/> to <see cref="IServiceCollection"/>
        /// </summary>
        /// <typeparam name="TInput1"></typeparam>
        /// <typeparam name="TInput2"></typeparam>
        /// <typeparam name="TOutput1"></typeparam>
        /// <typeparam name="TOutput2"></typeparam>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="item1Resolver"><see cref="Resolver{TInput1}"/></param>
        /// <param name="item2Resolver"><see cref="Resolver{TInput2}"/></param>
        /// <param name="finalResolver"><see cref="Resolver{TOutput1}"/></param>
        public static void AddCluster<TInput1, TInput2, TOutput1, TOutput2>(this IServiceCollection services,
            Func<IServiceProvider, Item1RemoteFuncPartialResolver<TInput1, TOutput1>> item1Resolver,
            Func<IServiceProvider, Item2RemoteFuncPartialResolver<TInput2, TOutput2>> item2Resolver,
            Func<IServiceProvider, DualFuncRemoteResolver<TOutput1, TOutput2>> finalResolver)
        {
            services.AddMemoryCache();
            services.TryAddSingleton(item1Resolver);
            services.TryAddSingleton(item2Resolver);
            services.TryAddSingleton(finalResolver);
            services.TryAddSingleton<ICluster<TInput1, TInput2>, Cluster<TInput1, TInput2, TOutput1, TOutput2>>();
            services.TryAddSingleton<IExposeMetrics>(sp => sp.GetRequiredService<ICluster<TInput1, TInput2>>());
        }
    }
}