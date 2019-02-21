using Microsoft.ServiceFabric.Data;
using System.IO;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public sealed class CacheStoreMetadata
    {
        public CacheStoreMetadata(int size, string firstCacheKey, string lastCacheKey)
        {
            Size = size;
            FirstCacheKey = firstCacheKey;
            LastCacheKey = lastCacheKey;
        }

        public int Size { get; private set; }
        public string FirstCacheKey { get; private set; }
        public string LastCacheKey { get; private set; }
    }

    class CacheStoreMetadataSerializer : IStateSerializer<CacheStoreMetadata>
    {
        CacheStoreMetadata IStateSerializer<CacheStoreMetadata>.Read(BinaryReader reader)
        {
            return new CacheStoreMetadata(
                reader.ReadInt32(),
                GetStringValueOrNull(reader.ReadString()),
                GetStringValueOrNull(reader.ReadString())
                );
        }

        void IStateSerializer<CacheStoreMetadata>.Write(CacheStoreMetadata value, BinaryWriter writer)
        {
            writer.Write(value.Size);
            writer.Write(value.FirstCacheKey ?? string.Empty);
            writer.Write(value.LastCacheKey ?? string.Empty);
        }

        // Read overload for differential de-serialization
        CacheStoreMetadata IStateSerializer<CacheStoreMetadata>.Read(CacheStoreMetadata baseValue, BinaryReader reader)
        {
            return ((IStateSerializer<CacheStoreMetadata>)this).Read(reader);
        }

        // Write overload for differential serialization
        void IStateSerializer<CacheStoreMetadata>.Write(CacheStoreMetadata baseValue, CacheStoreMetadata newValue, BinaryWriter writer)
        {
            ((IStateSerializer<CacheStoreMetadata>)this).Write(newValue, writer);
        }

        private string GetStringValueOrNull(string value)
        {
            return value == string.Empty ? null : value;
        }
    }
}
