using AutoFixture.Xunit2;
using Microsoft.Extensions.Internal;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Moq;
using SoCreate.Extensions.Caching.ServiceFabric;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SoCreate.Extensions.Caching.Tests
{
    public class DistributedCacheStoreServiceTest
    {
        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesNotExist_NullResultReturned(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);
            var expireTime = currentTime.AddSeconds(1);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, metadataDict);
            SetupInMemoryStores(stateManager, cacheItemDict);

            var result = await cacheStore.GetCachedItemAsync("keyThatDoesNotExist");
            Assert.Null(result);
        }

        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesHaveKeyAndIsIsNotAbsoluteExpired_CachedItemReturned(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);
            var expireTime = currentTime.AddSeconds(1);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, metadataDict);
            SetupInMemoryStores(stateManager, cacheItemDict);

            await cacheStore.SetCachedItemAsync("mykey", cacheValue, null, expireTime);
            var result = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Equal(cacheValue, result);
        }

        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesHaveKeyAndIsIsAbsoluteExpired_NullResultReturned(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);
            var expireTime = currentTime.AddSeconds(-1);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, metadataDict);
            SetupInMemoryStores(stateManager, cacheItemDict);

            await cacheStore.SetCachedItemAsync("mykey", cacheValue, null, expireTime);
            var result = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Null(result);
        }

        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesHaveKeyAndIsIsAbsoluteExpiredDoesNotSlideTime_ExpireTimeDoesNotSlide(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);
            var expireTime = currentTime.AddSeconds(5);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, metadataDict);
            SetupInMemoryStores(stateManager, cacheItemDict);

            await cacheStore.SetCachedItemAsync("mykey", cacheValue, null, expireTime);
            var result = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Equal(cacheValue, result);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime.AddSeconds(5));

            var resultAfter6Seconds = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Null(resultAfter6Seconds);
        }

        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesHaveKeyAndIsIsNotSlidingExpired_CachedItemReturned(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, metadataDict);
            SetupInMemoryStores(stateManager, cacheItemDict);

            await cacheStore.SetCachedItemAsync("mykey", cacheValue, TimeSpan.FromSeconds(1), null);
            var result = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Equal(cacheValue, result);
        }


        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesHaveKeyAndIsIsSlidingExpired_NullResultReturned(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, metadataDict);
            SetupInMemoryStores(stateManager, cacheItemDict);

            await cacheStore.SetCachedItemAsync("mykey", cacheValue, TimeSpan.FromSeconds(1), null);
            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime.AddSeconds(2));
            var result = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Null(result);
        }

        [Theory, AutoMoqData]
        async void GetCachedItemAsync_GetItemThatDoesHaveKeyAndIsIsSlidingExpired_SlidedExpirationUpdates(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            SetupInMemoryStores(stateManager, cacheItemDict);
            SetupInMemoryStores(stateManager, metadataDict);

            await cacheStore.SetCachedItemAsync("mykey", cacheValue, TimeSpan.FromSeconds(10), null);
            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime.AddSeconds(5));
            var resultAfter5Seconds = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Equal(cacheValue, resultAfter5Seconds);
            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime.AddSeconds(8));
            var resultAfter8Seconds = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Equal(cacheValue, resultAfter8Seconds);
            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime.AddSeconds(9));
            var resultAfter9Seconds = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Equal(cacheValue, resultAfter9Seconds);
            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime.AddSeconds(19));
            var resultAfter19Seconds = await cacheStore.GetCachedItemAsync("mykey");
            Assert.Null(resultAfter19Seconds);
        }

        [Theory, AutoMoqData]
        async void SetCachedItemAsync_AddItemsToCreateLinkedDictionary_DictionaryCreatedWithItemsLinked(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            var cachedItems = SetupInMemoryStores(stateManager, cacheItemDict);
            var metadata = SetupInMemoryStores(stateManager, metadataDict);

            await cacheStore.SetCachedItemAsync("1", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("2", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("3", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("4", cacheValue, TimeSpan.FromSeconds(10), null);

            Assert.Null(cachedItems["1"].BeforeCacheKey);
            foreach (var item in cachedItems)
            {
                if (item.Value.BeforeCacheKey != null)
                {
                    Assert.Equal(item.Key, cachedItems[item.Value.BeforeCacheKey].AfterCacheKey);
                }
                if (item.Value.AfterCacheKey != null)
                {
                    Assert.Equal(item.Key, cachedItems[item.Value.AfterCacheKey].BeforeCacheKey);
                }
            }
            Assert.Null(cachedItems["4"].AfterCacheKey);

            Assert.Equal("1", metadata["CacheStoreMetadata"].FirstCacheKey);
            Assert.Equal("4", metadata["CacheStoreMetadata"].LastCacheKey);
            Assert.Equal((cacheValue.Length + 250) * cachedItems.Count, metadata["CacheStoreMetadata"].Size);
        }

        [Theory, AutoMoqData]
        async void RemoveCachedItemAsync_RemoveItemsFromLinkedDictionary_ListStaysLinkedTogetherAfterItemsRemoved(
            [Frozen]Mock<IReliableStateManagerReplica> stateManager,
            [Frozen]Mock<IReliableDictionary<string, CachedItem>> cacheItemDict,
            [Frozen]Mock<IReliableDictionary<string, CacheStoreMetadata>> metadataDict,
            [Frozen]Mock<ISystemClock> systemClock,
            [Greedy]ServiceFabricDistributedCacheStoreService cacheStore)
        {
            var cacheValue = Encoding.UTF8.GetBytes("someValue");
            var currentTime = new DateTime(2019, 2, 1, 1, 0, 0);

            systemClock.SetupGet(m => m.UtcNow).Returns(currentTime);

            var cachedItems = SetupInMemoryStores(stateManager, cacheItemDict);
            var metadata = SetupInMemoryStores(stateManager, metadataDict);

            await cacheStore.SetCachedItemAsync("1", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("2", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("3", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("4", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("5", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("6", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("7", cacheValue, TimeSpan.FromSeconds(10), null);
            await cacheStore.SetCachedItemAsync("8", cacheValue, TimeSpan.FromSeconds(10), null);

            await cacheStore.RemoveCachedItemAsync("3");
            await cacheStore.RemoveCachedItemAsync("4");
            await cacheStore.RemoveCachedItemAsync("8");
            await cacheStore.RemoveCachedItemAsync("1");

            Assert.Null(cachedItems["2"].BeforeCacheKey);
            foreach (var item in cachedItems)
            {
                if (item.Value.BeforeCacheKey != null)
                {
                    Assert.Equal(item.Key, cachedItems[item.Value.BeforeCacheKey].AfterCacheKey);
                }
                if (item.Value.AfterCacheKey != null)
                {
                    Assert.Equal(item.Key, cachedItems[item.Value.AfterCacheKey].BeforeCacheKey);
                }
            }
            Assert.Null(cachedItems["7"].AfterCacheKey);

            Assert.Equal("2", metadata["CacheStoreMetadata"].FirstCacheKey);
            Assert.Equal("7", metadata["CacheStoreMetadata"].LastCacheKey);
            Assert.Equal((cacheValue.Length + 250) * cachedItems.Count, metadata["CacheStoreMetadata"].Size);
        }

        private Dictionary<TKey, TValue> SetupInMemoryStores<TKey, TValue>(Mock<IReliableStateManagerReplica> stateManager, Mock<IReliableDictionary<TKey, TValue>> reliableDict) where TKey : IComparable<TKey>, IEquatable<TKey>
        {
            var inMemoryDict = new Dictionary<TKey, TValue>();
            Func<TKey, ConditionalValue<TValue>> getItem = (key) => inMemoryDict.ContainsKey(key) ? new ConditionalValue<TValue>(true, inMemoryDict[key]) : new ConditionalValue<TValue>(false, default(TValue));

            stateManager.Setup(m => m.GetOrAddAsync<IReliableDictionary<TKey, TValue>>(It.IsAny<string>())).Returns(Task.FromResult(reliableDict.Object));
            reliableDict.Setup(m => m.TryGetValueAsync(It.IsAny<ITransaction>(), It.IsAny<TKey>())).Returns((ITransaction t, TKey key) => Task.FromResult(getItem(key)));
            reliableDict.Setup(m => m.TryGetValueAsync(It.IsAny<ITransaction>(), It.IsAny<TKey>(), It.IsAny<LockMode>())).Returns((ITransaction t, TKey key, LockMode l) => Task.FromResult(getItem(key)));
            reliableDict.Setup(m => m.SetAsync(It.IsAny<ITransaction>(), It.IsAny<TKey>(), It.IsAny<TValue>())).Returns((ITransaction t, TKey key, TValue ci) => { inMemoryDict[key] = ci; return Task.CompletedTask; });
            reliableDict.Setup(m => m.TryRemoveAsync(It.IsAny<ITransaction>(), It.IsAny<TKey>())).Returns((ITransaction t, TKey key) => { var r = getItem(key); inMemoryDict.Remove(key); return Task.FromResult(r); });

            return inMemoryDict;
        }

        class ServiceFabricDistributedCacheStoreService : DistributedCacheStoreService
        {
            public ServiceFabricDistributedCacheStoreService(StatefulServiceContext context, IReliableStateManagerReplica replica, ISystemClock clock) : base(context, replica, clock, (m) => { })
            {
            }
        }
    }
}
