using Microsoft.Extensions.Options;
using System;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public class ServiceFabricCacheOptions : IOptions<ServiceFabricCacheOptions>
    {
        public ServiceFabricCacheOptions Value => this;

        public Uri CacheStoreServiceUri { get; set; }
        public string CacheStoreEndpointName { get; set; }
        public Guid CacheStoreId { get; set; }
        public TimeSpan? RetryTimeout { get; set; }
    }
}