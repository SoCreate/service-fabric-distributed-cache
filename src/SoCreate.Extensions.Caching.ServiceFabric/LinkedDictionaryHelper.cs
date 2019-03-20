using Microsoft.ServiceFabric.Data;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
[assembly: InternalsVisibleTo("SoCreate.Extensions.Caching.Tests")]

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    
    class LinkedDictionaryHelper
    {
        private readonly Func<string, Task<ConditionalValue<CachedItem>>> _getCacheItem;
        private readonly int _byteSizeOffset;

        public LinkedDictionaryHelper(Func<string, Task<ConditionalValue<CachedItem>>> getCacheItem) : this(getCacheItem, 0)
        {
        }

        public LinkedDictionaryHelper(Func<string, Task<ConditionalValue<CachedItem>>> getCacheItem, int byteSizeOffset)
        {
            _getCacheItem = getCacheItem;
            _byteSizeOffset = byteSizeOffset;
        }
       
        public async Task<LinkedDictionaryItemsChanged> Remove(CacheStoreMetadata cacheStoreMetadata, CachedItem cachedItem)
        {
            var before = cachedItem.BeforeCacheKey;
            var after = cachedItem.AfterCacheKey;
            var size = (cacheStoreMetadata.Size - cachedItem.Value.Length) - _byteSizeOffset;

            // only item in linked dictionary
            if (before == null && after == null)
            {
                return new LinkedDictionaryItemsChanged(new Dictionary<string, CachedItem>(), new CacheStoreMetadata(size, null, null));
            }

            // first item in linked dictionary
            if (before == null)
            {
                var afterCachedItem = (await _getCacheItem(after)).Value;
                var newCachedItem = new Dictionary<string, CachedItem> { { after, new CachedItem(afterCachedItem.Value, null, afterCachedItem.AfterCacheKey, afterCachedItem.SlidingExpiration, afterCachedItem.AbsoluteExpiration) } };
                return new LinkedDictionaryItemsChanged(newCachedItem, new CacheStoreMetadata(size, after, cacheStoreMetadata.LastCacheKey));
            }

            // last item in linked dictionary
            if (after == null)
            {
                var beforeCachedItem = (await _getCacheItem(before)).Value;
                var newCachedItem = new Dictionary<string, CachedItem> { { before, new CachedItem(beforeCachedItem.Value, beforeCachedItem.BeforeCacheKey, null, beforeCachedItem.SlidingExpiration, beforeCachedItem.AbsoluteExpiration) } };
                return new LinkedDictionaryItemsChanged(newCachedItem, new CacheStoreMetadata(size, cacheStoreMetadata.FirstCacheKey, before));
            }

            // middle item in linked dictionary

            var beforeItem = (await _getCacheItem(before)).Value;
            var afterItem = (await _getCacheItem(after)).Value;

            var metadata = new CacheStoreMetadata(size, cacheStoreMetadata.FirstCacheKey, cacheStoreMetadata.LastCacheKey);

            var newCachedItems = new Dictionary<string, CachedItem>();
            // add new before cached item
            newCachedItems.Add(before, new CachedItem(beforeItem.Value, beforeItem.BeforeCacheKey, after, beforeItem.SlidingExpiration, beforeItem.AbsoluteExpiration));
            // add new after cached item
            newCachedItems.Add(after, new CachedItem(afterItem.Value, before, afterItem.AfterCacheKey, afterItem.SlidingExpiration, afterItem.AbsoluteExpiration));

            return new LinkedDictionaryItemsChanged(newCachedItems, metadata);
        }

        public async Task<LinkedDictionaryItemsChanged> AddLast(CacheStoreMetadata cacheStoreMetadata, string cacheItemKey, CachedItem cachedItem, byte[] newValue)
        {
            var cachedDictionary = new Dictionary<string, CachedItem>();
            var firstCacheKey = cacheItemKey;

            // set current last item to be the second from last
            if (cacheStoreMetadata.LastCacheKey != null)
            {
                var currentLastCacheItem = (await _getCacheItem(cacheStoreMetadata.LastCacheKey)).Value;
                firstCacheKey = cacheStoreMetadata.FirstCacheKey;
                cachedDictionary.Add(cacheStoreMetadata.LastCacheKey, new CachedItem(currentLastCacheItem.Value, currentLastCacheItem.BeforeCacheKey, cacheItemKey, currentLastCacheItem.SlidingExpiration, currentLastCacheItem.AbsoluteExpiration));
            }

            // set new cached item to be last item in list
            cachedDictionary.Add(cacheItemKey, new CachedItem(newValue, cacheStoreMetadata.LastCacheKey, null, cachedItem.SlidingExpiration, cachedItem.AbsoluteExpiration));

            // calculate size of new collection 
            var size = (cacheStoreMetadata.Size + newValue.Length) + _byteSizeOffset;

            // set new last item in the metadata
            var newCacheStoreMetadata = new CacheStoreMetadata(size, firstCacheKey, cacheItemKey);

            return new LinkedDictionaryItemsChanged(cachedDictionary, newCacheStoreMetadata);
        }
    }

    class LinkedDictionaryItemsChanged
    {
        public LinkedDictionaryItemsChanged(Dictionary<string, CachedItem> cachedItemsToUpdate, CacheStoreMetadata cacheStoreMetadata)
        {
            CachedItemsToUpdate = cachedItemsToUpdate;
            CacheStoreMetadata = cacheStoreMetadata;
        }

        public IReadOnlyDictionary<string, CachedItem> CachedItemsToUpdate { get; private set; }
        public CacheStoreMetadata CacheStoreMetadata { get; private set; }
    }
}
