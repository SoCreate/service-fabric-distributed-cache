using System.Fabric;
using SoCreate.Extensions.Caching.ServiceFabric;

namespace DistributedCacheStore
{
    internal sealed partial class DistributedCacheStore : DistributedCacheStoreService
    {
        public DistributedCacheStore(StatefulServiceContext context)
            : base(context, (message) => ServiceEventSource.Current.ServiceMessage(context, message))
        { }

        protected override int MaxCacheSizeInMegabytes => 1000;
    }
}
