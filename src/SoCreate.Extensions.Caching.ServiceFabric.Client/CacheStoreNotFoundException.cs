using System;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    class CacheStoreNotFoundException : Exception
    {
        public CacheStoreNotFoundException(string message) : base(message)
        {

        }
    }
}
