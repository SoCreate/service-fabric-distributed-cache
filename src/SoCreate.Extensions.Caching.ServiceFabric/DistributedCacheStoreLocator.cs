using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using System;
using System.Collections.Concurrent;
using System.Fabric;
using System.Fabric.Description;
using System.Fabric.Query;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    class DistributedCacheStoreLocator : IDistributedCacheStoreLocator
    {
        private const string CacheStoreProperty = "CacheStore";
        private const string CacheStorePropertyValue = "true";
        private const string ListenerName = "CacheStoreServiceListener";
        private Uri _serviceUri;
        private readonly string _endpointName;
        private readonly FabricClient _fabricClient;
        private ServicePartitionList _partitionList;
        private readonly ConcurrentDictionary<Guid, IServiceFabricCacheStoreService> _cacheStores;

        public DistributedCacheStoreLocator(IOptions<ServiceFabricCacheOptions> options)
        {
            var fabricOptions = options.Value;
            _serviceUri = fabricOptions.CacheStoreServiceUri;
            _endpointName = fabricOptions.CacheStoreEndpointName ?? ListenerName;
                       
            _fabricClient = new FabricClient();
            _cacheStores = new ConcurrentDictionary<Guid, IServiceFabricCacheStoreService>();
        }

        public async Task<IServiceFabricCacheStoreService> GetCacheStoreProxy(string cacheKey)
        {
            // Try to locate a cache store if one is not configured
            if (_serviceUri == null)
            {
                _serviceUri = await LocateCacheStoreAsync();
                if (_serviceUri == null)
                {
                    throw new CacheStoreNotFoundException("Cache store not found in Service Fabric cluster.  Try setting the 'CacheStoreServiceUri' configuration option to the location of your cache store.");
                }
            }

            var partitionInformation = await GetPartitionInformationForCacheKey(cacheKey);

            return _cacheStores.GetOrAdd(partitionInformation.Id, key => {
                var info = (Int64RangePartitionInformation)partitionInformation;
                var resolvedPartition = new ServicePartitionKey(info.LowKey);

                var proxyFactory = new ServiceProxyFactory((c) =>
                {
                    return new FabricTransportServiceRemotingClientFactory();
                });

                return proxyFactory.CreateServiceProxy<IServiceFabricCacheStoreService>(_serviceUri, resolvedPartition, Microsoft.ServiceFabric.Services.Communication.Client.TargetReplicaSelector.Default, _endpointName);
            });
        }

        private async Task<ServicePartitionInformation> GetPartitionInformationForCacheKey(string cacheKey)
        {
            var md5 = MD5.Create();
            var value = md5.ComputeHash(Encoding.ASCII.GetBytes(cacheKey));
            var key = BitConverter.ToInt64(value, 0);

            if (_partitionList == null)
            {
                _partitionList = await _fabricClient.QueryManager.GetPartitionListAsync(_serviceUri);
            }

            var partition = _partitionList.Single(p => ((Int64RangePartitionInformation)p.PartitionInformation).LowKey <= key && ((Int64RangePartitionInformation)p.PartitionInformation).HighKey >= key);
            return partition.PartitionInformation;
        }



        private async Task<Uri> LocateCacheStoreAsync()
        {
            try
            {
                bool hasPages = true;
                var query = new ApplicationQueryDescription() { MaxResults = 50 };

                while (hasPages)
                {
                    var apps = await _fabricClient.QueryManager.GetApplicationPagedListAsync(query);

                    query.ContinuationToken = apps.ContinuationToken;

                    hasPages = !string.IsNullOrEmpty(query.ContinuationToken);

                    foreach (var app in apps)
                    {
                        var serviceName = await LocateCacheStoreServiceInApplicationAsync(app.ApplicationName);
                        if (serviceName != null)
                            return serviceName;
                    }
                }
            }
            catch { }

            return null;
        }

        private async Task<Uri> LocateCacheStoreServiceInApplicationAsync(Uri applicationName)
        {
            try
            {
                bool hasPages = true;
                var query = new ServiceQueryDescription(applicationName) { MaxResults = 50 };

                while (hasPages)
                {
                    var services = await _fabricClient.QueryManager.GetServicePagedListAsync(query);

                    query.ContinuationToken = services.ContinuationToken;

                    hasPages = !string.IsNullOrEmpty(query.ContinuationToken);

                    foreach (var service in services)
                    {
                        var found = await IsCacheStore(service.ServiceName);
                        if (found)
                            return service.ServiceName;
                    }
                }
            }
            catch { }

            return null;
        }

        private async Task<bool> IsCacheStore(Uri serviceName)
        {
            try
            {
                var isCacheStore = await _fabricClient.PropertyManager.GetPropertyAsync(serviceName, CacheStoreProperty);
                return isCacheStore.GetValue<string>() == CacheStorePropertyValue;
            }
            catch { }

            return false;
        }
    }
}
