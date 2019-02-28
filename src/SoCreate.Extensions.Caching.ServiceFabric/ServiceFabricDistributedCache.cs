using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SoCreate.Extensions.Caching.Tests")]
namespace SoCreate.Extensions.Caching.ServiceFabric
{
    class ServiceFabricDistributedCache : IDistributedCache
    {
        private readonly IDistributedCacheStoreLocator _distributedCacheStoreLocator;
        private readonly ISystemClock _systemClock;
        private readonly Guid _cacheStoreId;

        public ServiceFabricDistributedCache(IOptions<ServiceFabricCacheOptions> options, IDistributedCacheStoreLocator distributedCacheStoreLocator, ISystemClock systemClock)
        {
            _cacheStoreId = options.Value.CacheStoreId;
            _distributedCacheStoreLocator = distributedCacheStoreLocator;
            _systemClock = systemClock;
        }

        public byte[] Get(string key)
        {
            return GetAsync(key).Result;
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            key = FormatCacheKey(key);
            var proxy = await _distributedCacheStoreLocator.GetCacheStoreProxy(key).ConfigureAwait(false);
            return await proxy.GetCachedItemAsync(key).ConfigureAwait(false);
        }

        public void Refresh(string key)
        {
            RefreshAsync(key).Wait();
        }

        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            await GetAsync(key, token);
        }

        public void Remove(string key)
        {
            RemoveAsync(key).Wait();
        }

        public async Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            key = FormatCacheKey(key);
            var proxy = await _distributedCacheStoreLocator.GetCacheStoreProxy(key).ConfigureAwait(false);
            await proxy.RemoveCachedItemAsync(key).ConfigureAwait(false);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetAsync(key, value, options).Wait();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            var absoluteExpireTime = GetAbsoluteExpiration(_systemClock.UtcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpireTime);

            key = FormatCacheKey(key);
            var proxy = await _distributedCacheStoreLocator.GetCacheStoreProxy(key).ConfigureAwait(false);
            await proxy.SetCachedItemAsync(key, value, options.SlidingExpiration, absoluteExpireTime).ConfigureAwait(false);
        }

        private DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
        {
            var expireTime = new DateTimeOffset?();
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
                expireTime = new DateTimeOffset?(utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value));
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                expireTime = new DateTimeOffset?(options.AbsoluteExpiration.Value);
            }
            return expireTime;
        }

        private void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
                throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
        }

        private string FormatCacheKey(string key)
        {
            return $"{_cacheStoreId}-{key}";
        }
    }
}
