using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Internal;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public abstract class DistributedCacheStoreService : StatefulService, IServiceFabricCacheStoreService
    {
        private const string CacheStoreProperty = "CacheStore";
        private const string CacheStorePropertyValue = "true";
        const int BytesInMegabyte = 1048576;
        const int ByteSizeOffset = 250;
        const int DefaultCacheSizeInMegabytes = 100;
        const string CacheStoreName = "CacheStore";
        const string CacheStoreMetadataName = "CacheStoreMetadata";
        const string CacheStoreMetadataKey = "CacheStoreMetadata";
        private const string ListenerName = "CacheStoreServiceListener";
        private readonly Uri _serviceUri;
        private readonly IReliableStateManagerReplica2 _reliableStateManagerReplica;
        private readonly Action<string> _log;
        private readonly ISystemClock _systemClock;
        private int _partitionCount = 1;

        public DistributedCacheStoreService(StatefulServiceContext context, Action<string> log = null)
            : base(context)
        {
            _serviceUri = context.ServiceName;
            _log = log;
            _systemClock = new SystemClock();

            if (!StateManager.TryAddStateSerializer(new CachedItemSerializer()))
            {
                throw new InvalidOperationException("Failed to set CachedItem custom serializer");
            }

            if (!StateManager.TryAddStateSerializer(new CacheStoreMetadataSerializer()))
            {
                throw new InvalidOperationException("Failed to set CacheStoreMetadata custom serializer");
            }
        }

        public DistributedCacheStoreService(StatefulServiceContext context, IReliableStateManagerReplica2 reliableStateManagerReplica, ISystemClock systemClock, Action<string> log)
            : base(context, reliableStateManagerReplica)
        {
            _serviceUri = context.ServiceName;
            _reliableStateManagerReplica = reliableStateManagerReplica;
            _log = log;
            _systemClock = systemClock;
        }

        protected async override Task OnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellationToken)
        {
            var client = new FabricClient();
            await client.PropertyManager.PutPropertyAsync(_serviceUri, CacheStoreProperty, CacheStorePropertyValue);
            _partitionCount = (await client.QueryManager.GetPartitionListAsync(_serviceUri)).Count;
        }

        protected virtual int MaxCacheSizeInMegabytes { get { return DefaultCacheSizeInMegabytes; } }

        public async Task<byte[]> GetCachedItemAsync(string key)
        {
            var cacheStore = await StateManager.GetOrAddAsync<IReliableDictionary<string, CachedItem>>(CacheStoreName);

            var cacheResult = await RetryHelper.ExecuteWithRetry(StateManager, async (tx, cancellationToken, state) =>
            {
                _log?.Invoke($"Get cached item called with key: {key} on partition id: {Partition?.PartitionInfo.Id}");
                return await cacheStore.TryGetValueAsync(tx, key);
            });

            if (cacheResult.HasValue)
            {
                var cachedItem = cacheResult.Value;
                var expireTime = cachedItem.AbsoluteExpiration;

                // cache item not expired
                if (_systemClock.UtcNow < expireTime)
                {
                    await SetCachedItemAsync(key, cachedItem.Value, cachedItem.SlidingExpiration, cachedItem.AbsoluteExpiration);
                    return cachedItem.Value;
                }
            }

            return null;
        }
        
        public async Task SetCachedItemAsync(string key, byte[] value, TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (slidingExpiration.HasValue)
            {
                var now = _systemClock.UtcNow;
                absoluteExpiration = now.AddMilliseconds(slidingExpiration.Value.TotalMilliseconds);                
            }

            var cacheStore = await StateManager.GetOrAddAsync<IReliableDictionary<string, CachedItem>>(CacheStoreName);
            var cacheStoreMetadata = await StateManager.GetOrAddAsync<IReliableDictionary<string, CacheStoreMetadata>>(CacheStoreMetadataName);

            await RetryHelper.ExecuteWithRetry(StateManager, async (tx, cancellationToken, state) => 
            {
                _log?.Invoke($"Set cached item called with key: {key} on partition id: {Partition?.PartitionInfo.Id}");
           
                Func<string, Task<ConditionalValue<CachedItem>>> getCacheItem = async (string cacheKey) => await cacheStore.TryGetValueAsync(tx, cacheKey, LockMode.Update);
                var linkedDictionaryHelper = new LinkedDictionaryHelper(getCacheItem, ByteSizeOffset);

                var cacheStoreInfo = (await cacheStoreMetadata.TryGetValueAsync(tx, CacheStoreMetadataKey, LockMode.Update)).Value ?? new CacheStoreMetadata(0, null, null);
                var existingCacheItem = (await getCacheItem(key)).Value;
                var cachedItem = ApplyAbsoluteExpiration(existingCacheItem, absoluteExpiration) ?? new CachedItem(value, null, null, slidingExpiration, absoluteExpiration);

                // empty linked dictionary
                if (cacheStoreInfo.FirstCacheKey == null)
                {
                    var metadata = new CacheStoreMetadata(value.Length + ByteSizeOffset, key, key);
                    await cacheStoreMetadata.SetAsync(tx, CacheStoreMetadataKey, metadata);
                    await cacheStore.SetAsync(tx, key, cachedItem);
                }
                else
                {
                    var cacheMetadata = cacheStoreInfo;

                    // linked node already exists in dictionary
                    if (existingCacheItem != null)
                    {
                        var removeResult = await linkedDictionaryHelper.Remove(cacheStoreInfo, cachedItem);
                        cacheMetadata = removeResult.CacheStoreMetadata;
                        await ApplyChanges(tx, cacheStore, cacheStoreMetadata, removeResult);
                    }

                    // add to last
                    var addLastResult = await linkedDictionaryHelper.AddLast(cacheMetadata, key, cachedItem, value);
                    await ApplyChanges(tx, cacheStore, cacheStoreMetadata, addLastResult);
                }
            });
        }

        public async Task RemoveCachedItemAsync(string key)
        {
            var cacheStore = await StateManager.GetOrAddAsync<IReliableDictionary<string, CachedItem>>(CacheStoreName);
            var cacheStoreMetadata = await StateManager.GetOrAddAsync<IReliableDictionary<string, CacheStoreMetadata>>(CacheStoreMetadataName);

            await RetryHelper.ExecuteWithRetry(StateManager, async (tx, cancellationToken, state) =>
            {
                _log?.Invoke($"Remove cached item called with key: {key} on partition id: {Partition?.PartitionInfo.Id}");

                var cacheResult = await cacheStore.TryRemoveAsync(tx, key);
                if (cacheResult.HasValue)
                {
                    Func<string, Task<ConditionalValue<CachedItem>>> getCacheItem = async (string cacheKey) => await cacheStore.TryGetValueAsync(tx, cacheKey, LockMode.Update);
                    var linkedDictionaryHelper = new LinkedDictionaryHelper(getCacheItem, ByteSizeOffset);

                    var cacheStoreInfo = (await cacheStoreMetadata.TryGetValueAsync(tx, CacheStoreMetadataKey, LockMode.Update)).Value ?? new CacheStoreMetadata(0, null, null);
                    var result = await linkedDictionaryHelper.Remove(cacheStoreInfo, cacheResult.Value);

                    await ApplyChanges(tx, cacheStore, cacheStoreMetadata, result);
                }
            });
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            yield return new ServiceReplicaListener(context =>
                new FabricTransportServiceRemotingListener(context, this), ListenerName);
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var cacheStore = await StateManager.GetOrAddAsync<IReliableDictionary<string, CachedItem>>(CacheStoreName);
            var cacheStoreMetadata = await StateManager.GetOrAddAsync<IReliableDictionary<string, CacheStoreMetadata>>(CacheStoreMetadataName);

            while (true)
            {
                await RemoveLeastRecentlyUsedCacheItemWhenOverMaxCacheSize(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }

        protected async Task RemoveLeastRecentlyUsedCacheItemWhenOverMaxCacheSize(CancellationToken cancellationToken)
        {
            var cacheStore = await StateManager.GetOrAddAsync<IReliableDictionary<string, CachedItem>>(CacheStoreName);
            var cacheStoreMetadata = await StateManager.GetOrAddAsync<IReliableDictionary<string, CacheStoreMetadata>>(CacheStoreMetadataName);
            bool continueRemovingItems = true;

            while (continueRemovingItems)
            {
                continueRemovingItems = false;
                cancellationToken.ThrowIfCancellationRequested();

                await RetryHelper.ExecuteWithRetry(StateManager, async (tx, cancelToken, state) =>
                {
                var metadata = await cacheStoreMetadata?.TryGetValueAsync(tx, CacheStoreMetadataKey, LockMode.Update);

                if (metadata.HasValue && !string.IsNullOrEmpty(metadata.Value.FirstCacheKey))
                {
                    _log?.Invoke($"Size: {metadata.Value.Size}  Max Size: {GetMaxSizeInBytes()}");

                        if (metadata.Value.Size > GetMaxSizeInBytes())
                        {
                            Func<string, Task<ConditionalValue<CachedItem>>> getCacheItem = async (string cacheKey) => await cacheStore.TryGetValueAsync(tx, cacheKey, LockMode.Update);
                            var linkedDictionaryHelper = new LinkedDictionaryHelper(getCacheItem, ByteSizeOffset);

                            var firstItemKey = metadata.Value.FirstCacheKey;

                            var firstCachedItem = (await getCacheItem(firstItemKey)).Value;

                            if (firstCachedItem != null)
                            {
                                // Move item to last item if cached item is not expired
                                if (firstCachedItem.AbsoluteExpiration > _systemClock.UtcNow)
                                {
                                    // remove cached item
                                    var removeResult = await linkedDictionaryHelper.Remove(metadata.Value, firstCachedItem);
                                    await ApplyChanges(tx, cacheStore, cacheStoreMetadata, removeResult);

                                    // add to last
                                    var addLastResult = await linkedDictionaryHelper.AddLast(removeResult.CacheStoreMetadata, firstItemKey, firstCachedItem, firstCachedItem.Value);
                                    await ApplyChanges(tx, cacheStore, cacheStoreMetadata, addLastResult);

                                    continueRemovingItems = addLastResult.CacheStoreMetadata.Size > GetMaxSizeInBytes();
                                }
                                else  // Remove 
                                {
                                    _log?.Invoke($"Auto Removing: {metadata.Value.FirstCacheKey}");

                                    var result = await linkedDictionaryHelper.Remove(metadata.Value, firstCachedItem);
                                    await ApplyChanges(tx, cacheStore, cacheStoreMetadata, result);
                                    await cacheStore.TryRemoveAsync(tx, metadata.Value.FirstCacheKey);

                                    continueRemovingItems = result.CacheStoreMetadata.Size > GetMaxSizeInBytes();
                                }
                            }
                        }
                    }
                });
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }

        private int GetMaxSizeInBytes()
        {
            return (MaxCacheSizeInMegabytes * BytesInMegabyte) / _partitionCount;
        }

        private async Task ApplyChanges(ITransaction tx, IReliableDictionary<string, CachedItem> cachedItemStore, IReliableDictionary<string, CacheStoreMetadata> cacheStoreMetadata, LinkedDictionaryItemsChanged linkedDictionaryItemsChanged)
        {
            foreach (var cacheItem in linkedDictionaryItemsChanged.CachedItemsToUpdate)
            {
                await cachedItemStore.SetAsync(tx, cacheItem.Key, cacheItem.Value);
            }
    
            await cacheStoreMetadata.SetAsync(tx, CacheStoreMetadataKey, linkedDictionaryItemsChanged.CacheStoreMetadata);
        }

        private CachedItem ApplyAbsoluteExpiration(CachedItem cachedItem, DateTimeOffset? absoluteExpiration)
        {
            if (cachedItem != null)
            {
                return new CachedItem(cachedItem.Value, cachedItem.BeforeCacheKey, cachedItem.AfterCacheKey, cachedItem.SlidingExpiration, absoluteExpiration);
            }
            return null;
        }
    }
}
