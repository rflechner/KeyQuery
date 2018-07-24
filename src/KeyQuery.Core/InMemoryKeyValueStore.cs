using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyQuery.Core
{
    public class InMemoryKeyValueStore<TKey, TValue> : IAsyncKeyValueStore<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> memory = new ConcurrentDictionary<TKey, TValue>();

        public ITransaction CreateTransaction() => new NullTransaction();

        public Task<TValue> AddOrUpdate(ITransaction tx, TKey key, TValue value, Func<TKey, TValue, TValue> updateWith)
            => Task.FromResult(memory.AddOrUpdate(key, value, updateWith));

        public Task<TValue> GetOrAdd(ITransaction tx, TKey key, Func<TKey, TValue> addWith)
            => Task.FromResult(memory.GetOrAdd(key, addWith));

        public Task<bool> TryAdd(ITransaction tx, TKey key, TValue value)
            => Task.FromResult(memory.TryAdd(key, value));

        public Task<(bool success, TValue value)> TryRemove(ITransaction tx, TKey key)
        {
            var r = memory.TryRemove(key, out var v);
            return Task.FromResult((r, v));
        }

        public Task<TValue> Get(ITransaction tx, TKey key)
            => Task.FromResult(memory[key]);

        public Task<ICollection<TKey>> AllKeys()
            => Task.FromResult(memory.Keys);

        public Task<ICollection<TValue>> AllValues()
            => Task.FromResult(memory.Values);
    }
}