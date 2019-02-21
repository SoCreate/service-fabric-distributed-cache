using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using System;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public static class ServiceFabricCachingServicesExtensions
    {
        public static IServiceCollection AddDistributedServiceFabricCache(this IServiceCollection services, Action<ServiceFabricCacheOptions> setupAction = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (setupAction == null) {
                setupAction = (s) => { };
            }

            services.AddOptions();
            services.Configure(setupAction);

            return services
                .AddSingleton<IDistributedCacheStoreLocator, DistributedCacheStoreLocator>()
                .AddSingleton<ISystemClock, SystemClock>()
                .AddSingleton<IDistributedCache, ServiceFabricDistributedCache>();
        }
    }
}
