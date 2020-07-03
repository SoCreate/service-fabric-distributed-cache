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
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.VisualStudio.Threading;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    class DistributedCacheStoreLocator : IDistributedCacheStoreLocator
    {
        private const string CacheStoreProperty = "CacheStore";
        private const string CacheStorePropertyValue = "true";
        private const string ListenerName = "CacheStoreServiceListener";
        private AsyncLazy<Uri> _serviceUri;
        private readonly string _endpointName;
        private readonly TimeSpan? _retryTimeout;
        private readonly FabricClient _fabricClient;
        private AsyncLazy<ServicePartitionList> _partitionList;
        private readonly ConcurrentDictionary<Guid, IServiceFabricCacheStoreService> _cacheStores;
        private readonly ServiceFabricCacheOptions _options;

        public DistributedCacheStoreLocator(IOptions<ServiceFabricCacheOptions> options)
        {
            _options = options.Value;
            _endpointName = _options.CacheStoreEndpointName ?? ListenerName;
            _retryTimeout = _options.RetryTimeout;
            _fabricClient = new FabricClient();
            _cacheStores = new ConcurrentDictionary<Guid, IServiceFabricCacheStoreService>();
            _serviceUri = new AsyncLazy<Uri>(LocateCacheStoreAsync);
            _partitionList = new AsyncLazy<ServicePartitionList>(GetPartitionListAsync);
        }

        public async Task<IServiceFabricCacheStoreService> GetCacheStoreProxy(string cacheKey)
        {
            var partitionInformation = await GetPartitionInformationForCacheKeyAsync(cacheKey);

            var serviceUri = await _serviceUri.GetValueAsync();

            return _cacheStores.GetOrAdd(partitionInformation.Id, key =>
            {
                var info = (Int64RangePartitionInformation)partitionInformation;
                var resolvedPartition = new ServicePartitionKey(info.LowKey);
                var retrySettings = _retryTimeout.HasValue ? new OperationRetrySettings(_retryTimeout.Value) : null;

                var proxyFactory = new ServiceProxyFactory((c) =>
                {
                    return new FabricTransportServiceRemotingClientFactory();
                }, retrySettings);

                return proxyFactory.CreateServiceProxy<IServiceFabricCacheStoreService>(serviceUri, resolvedPartition, TargetReplicaSelector.Default, _endpointName);
            });
        }

        private async Task<ServicePartitionInformation> GetPartitionInformationForCacheKeyAsync(string cacheKey)
        {
            var md5 = MD5.Create();
            var value = md5.ComputeHash(Encoding.ASCII.GetBytes(cacheKey));
            var key = BitConverter.ToInt64(value, 0);

            var partition = (await _partitionList.GetValueAsync()).Single(p => ((Int64RangePartitionInformation)p.PartitionInformation).LowKey <= key && ((Int64RangePartitionInformation)p.PartitionInformation).HighKey >= key);
            return partition.PartitionInformation;
        }

        private async Task<ServicePartitionList> GetPartitionListAsync()
        {
            return await _fabricClient.QueryManager.GetPartitionListAsync(await _serviceUri.GetValueAsync());
        }


        private async Task<Uri> LocateCacheStoreAsync()
        {
            if (_options.CacheStoreServiceUri != null)
            {
                return _options.CacheStoreServiceUri;
            }
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

            throw new CacheStoreNotFoundException("Cache store not found in Service Fabric cluster.  Try setting the 'CacheStoreServiceUri' configuration option to the location of your cache store."); ;
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
