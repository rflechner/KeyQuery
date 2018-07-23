using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyQuery.Core
{
    public interface IAsyncKeyValueStore<TKey, TValue>
    {
        Task<TValue> AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> updateWith);
        Task<TValue> GetOrAdd(TKey key, Func<TKey, TValue> addWith);
        Task<bool> TryAdd(TKey key, TValue value);
        Task<(bool success, TValue value)> TryRemove(TKey key);
        Task<TValue> Get(TKey key);
        Task<ICollection<TKey>> AllKeys();
        Task<ICollection<TValue>> AllValues();
    }
}