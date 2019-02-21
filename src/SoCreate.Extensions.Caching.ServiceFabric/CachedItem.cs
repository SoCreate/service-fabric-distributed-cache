using Microsoft.ServiceFabric.Data;
using System;
using System.IO;

namespace SoCreate.Extensions.Caching.ServiceFabric
{
    public sealed class CachedItem
    {
        public CachedItem(byte[] value, string beforeCacheKey = null, string afterCacheKey = null, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
        {
            Value = value;
            BeforeCacheKey = beforeCacheKey;
            AfterCacheKey = afterCacheKey;
            SlidingExpiration = slidingExpiration;
            AbsoluteExpiration = absoluteExpiration;
        }

        public byte[] Value { get; private set; }
        public string BeforeCacheKey { get; private set; }
        public string AfterCacheKey { get; private set; }
        public TimeSpan? SlidingExpiration { get; private set; }
        public DateTimeOffset? AbsoluteExpiration { get; private set; }
    }

    class CachedItemSerializer : IStateSerializer<CachedItem>
    {
        CachedItem IStateSerializer<CachedItem>.Read(BinaryReader reader)
        {
            var byteLength = reader.ReadInt32();
            return new CachedItem(
                reader.ReadBytes(byteLength),
                GetStringValueOrNull(reader.ReadString()),
                GetStringValueOrNull(reader.ReadString()),
                GetTimeSpanFromTicks(reader.ReadInt64()),
                GetDateTimeOffsetFromDateData(reader.ReadInt64(), reader.ReadInt64())
                );
        }

        void IStateSerializer<CachedItem>.Write(CachedItem value, BinaryWriter writer)
        {
            writer.Write(value.Value.Length);
            writer.Write(value.Value);
            writer.Write(value.BeforeCacheKey ?? string.Empty);
            writer.Write(value.AfterCacheKey ?? string.Empty);
            writer.Write(GetTicksFromTimeSpan(value.SlidingExpiration));
            writer.Write(GetLongDateTimeFromDateTimeOffset(value.AbsoluteExpiration));
            writer.Write(GetShortOffsetFromDateTimeOffset(value.AbsoluteExpiration));
        }

        // Read overload for differential de-serialization
        CachedItem IStateSerializer<CachedItem>.Read(CachedItem baseValue, BinaryReader reader)
        {
            return ((IStateSerializer<CachedItem>)this).Read(reader);
        }

        // Write overload for differential serialization
        void IStateSerializer<CachedItem>.Write(CachedItem baseValue, CachedItem newValue, BinaryWriter writer)
        {
            ((IStateSerializer<CachedItem>)this).Write(newValue, writer);
        }

        private string GetStringValueOrNull(string value)
        {
            return value == string.Empty ? null : value;
        }

        private TimeSpan? GetTimeSpanFromTicks(long ticks)
        {
            if (ticks == 0) return null;

            return TimeSpan.FromTicks(ticks);
        }

        private long GetTicksFromTimeSpan(TimeSpan? timeSpan)
        {
            if (!timeSpan.HasValue) return 0;

            return timeSpan.Value.Ticks;
        }

        private DateTimeOffset? GetDateTimeOffsetFromDateData(long dateDataTicks, long offsetTicks)
        {
            return new DateTimeOffset(DateTime.FromBinary(dateDataTicks), new TimeSpan(offsetTicks));
        }

        private long GetLongDateTimeFromDateTimeOffset(DateTimeOffset? dateTimeOffset)
        {
            if (!dateTimeOffset.HasValue) return 0;
            return dateTimeOffset.Value.Ticks;
        }

        private long GetShortOffsetFromDateTimeOffset(DateTimeOffset? dateTimeOffset)
        {
            if (!dateTimeOffset.HasValue) return 0;
            return dateTimeOffset.Value.Offset.Ticks;
        }
    }
}
