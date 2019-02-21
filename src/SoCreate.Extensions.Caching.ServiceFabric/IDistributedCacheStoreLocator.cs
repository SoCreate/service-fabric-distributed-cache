using System.Threading.Tasks;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    interface IDistributedCacheStoreLocator
    {
        Task<IServiceFabricCacheStoreService> GetCacheStoreProxy(string cacheKey);
    }
}