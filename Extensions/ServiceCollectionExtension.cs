using System.Collections;
using Microsoft.Extensions.DependencyInjection;

namespace OtlpServer.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddSingleton<TService1, TService2, TImplementation>(this IServiceCollection services)
            where TImplementation : class, TService1, TService2
            where TService1 : class
            where TService2 : class
        {
            services.AddSingleton<TImplementation>();
            services.AddSingleton<TService1>(p => p.GetRequiredService<TImplementation>());
            services.AddSingleton<TService2>(p => p.GetRequiredService<TImplementation>());

            return services;
        }
    }
}
