namespace SoCreate.Extensions.Caching.ServiceFabric
{
    class CacheStoreNotFoundException : CacheStoreException
    {
        internal CacheStoreNotFoundException(string message) : base(message)
        {

        }
    }
}
