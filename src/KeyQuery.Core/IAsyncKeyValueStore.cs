using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyQuery.Core
{
    public interface IAsyncKeyValueStore<TKey, TValue>
    {
        ITransaction CreateTransaction();
        Task<TValue> AddOrUpdate(ITransaction tx, TKey key, TValue value, Func<TKey, TValue, TValue> updateWith);
        Task<TValue> GetOrAdd(ITransaction tx, TKey key, Func<TKey, TValue> addWith);
        Task<bool> TryAdd(ITransaction tx, TKey key, TValue value);
        Task<(bool success, TValue value)> TryRemove(ITransaction tx, TKey key);
        Task<TValue> Get(ITransaction tx, TKey key);
        Task<ICollection<TKey>> AllKeys();
        Task<ICollection<TValue>> AllValues();
    }
}