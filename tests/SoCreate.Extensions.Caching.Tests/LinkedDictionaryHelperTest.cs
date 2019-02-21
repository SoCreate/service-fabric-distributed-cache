using AutoFixture.Xunit2;
using Microsoft.ServiceFabric.Data;
using Moq;
using SoCreate.Extensions.Caching.ServiceFabric;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SoCreate.Extensions.Caching.Tests
{
    public class LinkedDictionaryHelperTest
    {
        [Theory, AutoMoqData]
        async void AddLast_AddNewItemToEndOfList_LinkOldLastItemToNewLastItem(
            [Frozen]Mock<Func<string, Task<ConditionalValue<CachedItem>>>> getCacheItem,
            CacheStoreMetadata cacheStoreMetadata,
            ConditionalValue<CachedItem> cachedItem,
            CachedItem newCachedItem,
            LinkedDictionaryHelper linkedDictionaryHelper)
        {
            var newItemKey = "NewLastItem";
            var cachedValue = Encoding.UTF8.GetBytes("some value");
            var totalSize = cacheStoreMetadata.Size + cachedValue.Length;

            getCacheItem.Setup(mock => mock(It.IsAny<string>())).ReturnsAsync(await Task.FromResult(cachedItem));

            var result = await linkedDictionaryHelper.AddLast(cacheStoreMetadata, newItemKey, newCachedItem, cachedValue);
            Assert.Equal(2, result.CachedItemsToUpdate.Count);

            var oldLastItem = result.CachedItemsToUpdate[result.CachedItemsToUpdate[newItemKey].BeforeCacheKey];
            Assert.Equal(newItemKey, oldLastItem.AfterCacheKey);
            Assert.Equal(cachedItem.Value.Value, oldLastItem.Value);
            Assert.Equal(cachedItem.Value.SlidingExpiration, oldLastItem.SlidingExpiration);
            Assert.Equal(cachedItem.Value.AbsoluteExpiration, oldLastItem.AbsoluteExpiration);

            var newLastItem = result.CachedItemsToUpdate[newItemKey];

            Assert.Equal(cacheStoreMetadata.LastCacheKey, newLastItem.BeforeCacheKey);
            Assert.Null(newLastItem.AfterCacheKey);
            Assert.Equal(cachedValue, newLastItem.Value);
            Assert.Equal(newCachedItem.SlidingExpiration, newLastItem.SlidingExpiration);
            Assert.Equal(newCachedItem.AbsoluteExpiration, newLastItem.AbsoluteExpiration);
            Assert.Equal(totalSize, result.CacheStoreMetadata.Size);
            Assert.Equal(cacheStoreMetadata.FirstCacheKey, result.CacheStoreMetadata.FirstCacheKey);
            Assert.Equal(newItemKey, result.CacheStoreMetadata.LastCacheKey);
        }

        [Theory, AutoMoqData]
        async void AddLast_AddNewItemToEndOfEmptyList_ListContainsOnlyNewItem(
            [Frozen]Mock<Func<string, Task<ConditionalValue<CachedItem>>>> getCacheItem,
            ConditionalValue<CachedItem> cachedItem,
            CachedItem newCachedItem,
            LinkedDictionaryHelper linkedDictionaryHelper)
        {
            var cacheStoreMetadata = new CacheStoreMetadata(0, null, null);
            var newItemKey = "NewLastItem";
            var cachedValue = Encoding.UTF8.GetBytes("some value");
            var totalSize = cacheStoreMetadata.Size + cachedValue.Length;

            getCacheItem.Setup(mock => mock(It.IsAny<string>())).ReturnsAsync(await Task.FromResult(cachedItem));

            var result = await linkedDictionaryHelper.AddLast(cacheStoreMetadata, newItemKey, newCachedItem, cachedValue);
            Assert.Equal(1, result.CachedItemsToUpdate.Count);

            var newLastItem = result.CachedItemsToUpdate[newItemKey];

            Assert.Null(newLastItem.BeforeCacheKey);
            Assert.Null(newLastItem.AfterCacheKey);
            Assert.Equal(cachedValue, newLastItem.Value);
            Assert.Equal(newCachedItem.SlidingExpiration, newLastItem.SlidingExpiration);
            Assert.Equal(newCachedItem.AbsoluteExpiration, newLastItem.AbsoluteExpiration);
            Assert.Equal(totalSize, result.CacheStoreMetadata.Size);
            Assert.Equal(newItemKey, result.CacheStoreMetadata.FirstCacheKey);
            Assert.Equal(newItemKey, result.CacheStoreMetadata.LastCacheKey);
        }

        [Theory, AutoMoqData]
        async void Remove_OnlyItemInLinkedDictionary_SetCacheItemNotCalled(
            CacheStoreMetadata cacheStoreMetadata,
            LinkedDictionaryHelper linkedDictionaryHelper)
        {
            var cachedValue = Encoding.UTF8.GetBytes("some value");
            var totalSize = cacheStoreMetadata.Size - cachedValue.Length;
            var c = new CachedItem(cachedValue, null, null);

            var result = await linkedDictionaryHelper.Remove(cacheStoreMetadata, c);
            Assert.Empty(result.CachedItemsToUpdate);

            Assert.Equal(totalSize, result.CacheStoreMetadata.Size);
            Assert.Null(result.CacheStoreMetadata.FirstCacheKey);
            Assert.Null(result.CacheStoreMetadata.LastCacheKey);
        }

        [Theory, AutoMoqData]
        async void Remove_FirstItemInLinkedDictionary_SetSecondItemToBeFirst(
            [Frozen]Mock<Func<string, Task<ConditionalValue<CachedItem>>>> getCacheItem,
            CacheStoreMetadata cacheStoreMetadata,
            LinkedDictionaryHelper linkedDictionaryHelper)
        {
            var cachedValue = Encoding.UTF8.GetBytes("some value");
            var totalSize = cacheStoreMetadata.Size - cachedValue.Length;

            var items = new Dictionary<string, CachedItem> {
                { "1", new CachedItem(cachedValue, null, "2") },
                { "2", new CachedItem(cachedValue, "1", "3", TimeSpan.FromMilliseconds(100), new DateTime(1000)) },
                { "3", new CachedItem(cachedValue, "2", null) }
            };

            getCacheItem.Setup(mock => mock(It.IsAny<string>())).ReturnsAsync(await Task.FromResult(new ConditionalValue<CachedItem>(true, items["2"])));

            var result = await linkedDictionaryHelper.Remove(cacheStoreMetadata, items["1"]);
            Assert.Single(result.CachedItemsToUpdate);
            var newFirstItem = result.CachedItemsToUpdate["2"];

            Assert.Null(newFirstItem.BeforeCacheKey);
            Assert.Equal(items["2"].AfterCacheKey, newFirstItem.AfterCacheKey);
            Assert.Equal(cachedValue, newFirstItem.Value);
            Assert.Equal(items["2"].SlidingExpiration, newFirstItem.SlidingExpiration);
            Assert.Equal(items["2"].AbsoluteExpiration, newFirstItem.AbsoluteExpiration);

            Assert.Equal(totalSize, result.CacheStoreMetadata.Size);
            Assert.Equal("2", result.CacheStoreMetadata.FirstCacheKey);
            Assert.Equal(cacheStoreMetadata.LastCacheKey, result.CacheStoreMetadata.LastCacheKey);
        }

        [Theory, AutoMoqData]
        async void Remove_LastItemInLinkedDictionary_SetSecondItemFromLastToBeLast(
            [Frozen]Mock<Func<string, Task<ConditionalValue<CachedItem>>>> getCacheItem,
            CacheStoreMetadata cacheStoreMetadata,
            LinkedDictionaryHelper linkedDictionaryHelper)
        {
            var cachedValue = Encoding.UTF8.GetBytes("some value");
            var totalSize = cacheStoreMetadata.Size - cachedValue.Length;

            var items = new Dictionary<string, CachedItem> {
                { "1", new CachedItem(cachedValue, null, "2") },
                { "2", new CachedItem(cachedValue, "1", "3", TimeSpan.FromMilliseconds(100), new DateTime(1000)) },
                { "3", new CachedItem(cachedValue, "2", null) }
            };

            getCacheItem.Setup(mock => mock(It.IsAny<string>())).ReturnsAsync(await Task.FromResult(new ConditionalValue<CachedItem>(true, items["2"])));

            var result = await linkedDictionaryHelper.Remove(cacheStoreMetadata, items["3"]);
            Assert.Single(result.CachedItemsToUpdate);
            var newLastItem = result.CachedItemsToUpdate["2"];

            Assert.Equal(items["2"].BeforeCacheKey, newLastItem.BeforeCacheKey);
            Assert.Null(newLastItem.AfterCacheKey);
            Assert.Equal(cachedValue, newLastItem.Value);
            Assert.Equal(items["2"].SlidingExpiration, newLastItem.SlidingExpiration);
            Assert.Equal(items["2"].AbsoluteExpiration, newLastItem.AbsoluteExpiration);

            Assert.Equal(totalSize, result.CacheStoreMetadata.Size);
            Assert.Equal(cacheStoreMetadata.FirstCacheKey, result.CacheStoreMetadata.FirstCacheKey);
            Assert.Equal("2", result.CacheStoreMetadata.LastCacheKey);
        }

        [Theory, AutoMoqData]
        async void Remove_MiddleItemInLinkedDictionary_ItemBeforeAndAfterNeedTobeLinked(
            [Frozen]Mock<Func<string, Task<ConditionalValue<CachedItem>>>> getCacheItem,
            CacheStoreMetadata cacheStoreMetadata,
            LinkedDictionaryHelper linkedDictionaryHelper)
        {
            var cachedValue = Encoding.UTF8.GetBytes("some value");
            var totalSize = cacheStoreMetadata.Size - cachedValue.Length;

            var items = new Dictionary<string, CachedItem> {
                { "1", new CachedItem(cachedValue, null, "2", TimeSpan.FromMilliseconds(10), new DateTime(50)) },
                { "2", new CachedItem(cachedValue, "1", "3") },
                { "3", new CachedItem(cachedValue, "2", null, TimeSpan.FromMilliseconds(100), new DateTime(1000)) }
            };

            getCacheItem.Setup(mock => mock("1")).ReturnsAsync(await Task.FromResult(new ConditionalValue<CachedItem>(true, items["1"])));
            getCacheItem.Setup(mock => mock("3")).ReturnsAsync(await Task.FromResult(new ConditionalValue<CachedItem>(true, items["3"])));


            var result = await linkedDictionaryHelper.Remove(cacheStoreMetadata, items["2"]);
            Assert.Equal(2, result.CachedItemsToUpdate.Count);

            var newFirstItem = result.CachedItemsToUpdate["1"];
            Assert.Null(newFirstItem.BeforeCacheKey);
            Assert.Equal("3", newFirstItem.AfterCacheKey);
            Assert.Equal(cachedValue, newFirstItem.Value);
            Assert.Equal(items["1"].SlidingExpiration, newFirstItem.SlidingExpiration);
            Assert.Equal(items["1"].AbsoluteExpiration, newFirstItem.AbsoluteExpiration);

            var newLastItem = result.CachedItemsToUpdate["3"];
            Assert.Equal("1", newLastItem.BeforeCacheKey);
            Assert.Null(newLastItem.AfterCacheKey);
            Assert.Equal(cachedValue, newLastItem.Value);
            Assert.Equal(items["3"].SlidingExpiration, newLastItem.SlidingExpiration);
            Assert.Equal(items["3"].AbsoluteExpiration, newLastItem.AbsoluteExpiration);

            Assert.Equal(totalSize, result.CacheStoreMetadata.Size);
            Assert.Equal(cacheStoreMetadata.FirstCacheKey, result.CacheStoreMetadata.FirstCacheKey);
            Assert.Equal(cacheStoreMetadata.LastCacheKey, result.CacheStoreMetadata.LastCacheKey);
        }
    }
}
