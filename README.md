# Service Fabric Distributed Cache
An implementation of the IDistributedCache that uses a Stateful Reliable Service Fabric service to act as the cache store. You can use this library to setup a distributed cache and use Service Fabric instead of Redis or SQL Server.

## How To Setup
#### Setup the cache store
- Create a new .NET Core Stateful Service
- Install Nuget package
```
    dotnet add package SoCreate.Extensions.Caching.ServiceFabric
```
- Extend the new stateful service using "DistributedCacheStoreService" as the base class. (**note: You can also set the size of the cache store)
```
    public class DistributedCacheStore : DistributedCacheStoreService
    {
        public DistributedCacheStore(StatefulServiceContext context) : base(context)
        { }

        protected override int MaxCacheSizeInMegabytes => 1000;
    }
```
#### Setup client to use distributed cache
- Create your client .NET Core Reliable Service Fabric Service
- Install Nuget package
```
    dotnet add package SoCreate.Extensions.Caching.ServiceFabric
```
- Configure Service Fabric implementation of IDistributedCache with the Dependency Injection container
```
    public class Startup
    {
        
        ...

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDistributedServiceFabricCache();
        }

        ...

    }
```
## Example Usage
```
    [Route("api/[controller]")]
    [ApiController]
    public class CacheDemoController : ControllerBase
    {
        private readonly IDistributedCache _distributedCache;

        public CacheDemoController(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        [HttpGet("SetSlidingCacheItem")]
        public async Task<ActionResult<string>> SetSlidingCacheItem()
        {
            var options = new DistributedCacheEntryOptions();
            options.SlidingExpiration = TimeSpan.FromSeconds(20);

            await _distributedCache.SetAsync("SlidingCacheItem", Encoding.UTF8.GetBytes(DateTime.Now.ToString()), options);

            return new EmptyResult();
        }

        [HttpGet("GetSlidingCacheItem")]
        public async Task<ActionResult<string>> GetSlidingCacheItem()
        {
            var bytes = await _distributedCache.GetAsync("SlidingCacheItem");

            if (bytes != null)
                return Content(Encoding.UTF8.GetString(bytes));

            return new EmptyResult();
        }
    }
```
## Optional Configuration Settings

 [Optional] CacheStoreId - Used to uniquely identify an application in the Cache Store so that cache keys between different application do not conflict.

 [Optional] CacheStoreServiceUri - Used to explicitly point to the Stateful Reliable Service that is the cache store. If not supplied the client will try to auto discover the cache store.

[Optional] CacheStoreEndpointName - Used to explicitly specify the endpoint name that the Stateful Reliable Service cache store is listening on. If not supplied the client will use the default endpoint name for the cache store.

#### How to use configuration settings
```
    public class Startup
    {
        static readonly Guid CacheStoreId = new Guid("ec4ae77e-f015-4fe1-8735-5ae7a77385ef");

        ...

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDistributedServiceFabricCache(options => {
                options.CacheStoreId = CacheStoreId;
                options.CacheStoreServiceUri = new Uri("fabric:/ServiceFabricDistributedCache/DistributedCacheStore");
            });
        }

        ...

    }
```

## Requirements
- Use within a Service Fabric Cluster