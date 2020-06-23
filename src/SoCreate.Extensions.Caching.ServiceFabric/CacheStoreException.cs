using System;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public class CacheStoreException : Exception
    {
        internal CacheStoreException(string message) : base(message)
        {

        }

        internal CacheStoreException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
