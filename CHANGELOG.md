# 1.0.7 (2019-03-11)

<a name="1.0.7"></a>

### Bug Fixes
* **caching:** Fix issue with getting a cached item not working consistently in the senario where your cache store has multiple partitions
* **caching:** Add better error message when client can't find the cache store in the Service Fabric cluster

# 1.0.6 (2019-03-06)

<a name="1.0.6"></a>

### Bug Fixes
* **caching:** Fix issue with non-expired cached items getting removed from cache if cache is full and cached item was not recently retrieved
* **caching:** Fix issue with cache being cleared when cache store process was restarted

# 1.0.5 (2019-03-01)

<a name="1.0.5"></a>

### Bug Fixes
* **package information:** Update Nuget package information

# 1.0.4 (2019-02-28)

<a name="1.0.4"></a>

### Bug Fixes
* **caching:** Fix issue with Set not keeping cached items expiration settings
* **caching:** Fix issue with cache items set to absolute expiration not being updated as recently retrieved

# 1.0.1 (2019-02-26)

### Bug Fixes
* **package dependencies:** Fix dependencies for Nuget package

# 1.0.0 (2019-02-26)

<a name="1.0.0"></a>

### Initial Release
