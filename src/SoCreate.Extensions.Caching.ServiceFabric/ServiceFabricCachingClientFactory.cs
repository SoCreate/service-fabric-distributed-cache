namespace SoCreate.Extensions.Caching.ServiceFabric
{
    using System;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Internal;

    public static class ServiceFabricCachingClientFactory
    {
        public static IDistributedCache CreateCacheClient(Action<ServiceFabricCacheOptions> setupAction = null)
        {
            var options = new ServiceFabricCacheOptions();

            setupAction?.Invoke(options);

            IDistributedCacheStoreLocator locator = new DistributedCacheStoreLocator(options);
            ISystemClock clock = new SystemClock();
            return new ServiceFabricDistributedCache(options, locator, clock);
        }
    }
}