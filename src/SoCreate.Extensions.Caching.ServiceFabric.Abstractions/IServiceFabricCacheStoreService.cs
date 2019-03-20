using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Threading.Tasks;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public interface IServiceFabricCacheStoreService : IService
    {
        Task<byte[]> GetCachedItemAsync(string key);
        Task SetCachedItemAsync(string key, byte[] value, TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration);
        Task RemoveCachedItemAsync(string key);
    }
}
