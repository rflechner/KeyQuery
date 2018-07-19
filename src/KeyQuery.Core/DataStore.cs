using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace KeyQuery.Core
{
    public class DataStore<TId, T>
        where T : IDto<TId>
    {
        private readonly IAsyncKeyValueStore<TId, T> records;
        private readonly IndexStore<TId> indexes;
        private readonly IDictionary<string, Func<T, string>> indexValueProviders;

        public DataStore(IAsyncKeyValueStore<TId, T> records, IndexStore<TId> indexes, IDictionary<string, Func<T, string>> indexValueProviders)
        {
            this.records = records;
            this.indexes = indexes;
            this.indexValueProviders = indexValueProviders;
        }

        public IAsyncKeyValueStore<TId, T> Records => records;

        public IndexStore<TId> Indexes => indexes;

        public static async Task<DataStore<TId, T>> Build(
            Func<IAsyncKeyValueStore<TId, T>> buildPersistence,
            Func<string, Task<IAsyncKeyValueStore<FieldValue, ImmutableHashSet<TId>>>> buildFieldIndexPersistence,
            params Expression<Func<T, string>>[] indexedMembers)
        {
            var indexes = new IndexStore<TId>();
            var indexedFields = indexedMembers.Select(m =>
            {
                var path = m.GetIndexName();
                return (path, m.Compile());
            }).ToArray();

            foreach ((string name, Func<T, string> getter) in indexedFields)
            {
                indexes.Stores.TryAdd(name, await buildFieldIndexPersistence(name));
            }

            return new DataStore<TId, T>(buildPersistence(), indexes, Enumerable.ToDictionary(indexedFields, kv => kv.Item1, kv => kv.Item2));
        }

        public async Task<bool> Insert(T record)
        {
            if (!await records.TryAdd(record.Id, record))
                return false;

            foreach (var index in indexValueProviders)
            {
                var value = index.Value(record);
                await Index(record.Id, index.Key, value);
            }

            return false;
        }

        public async Task Index(TId id, FieldName name, object value)
        {
            var index = indexes.Stores[name];
            var fieldValue = value?.ToString();
            var currentIds = await index.GetOrAdd(fieldValue, _ => ImmutableHashSet<TId>.Empty);
            var ids = currentIds.Add(id);
            await index.AddOrUpdate(fieldValue, ids, (_, set) => ids);
        }

        public async Task<ICollection<T>> SearchByIndex(FieldName name, object value)
        {
            var index = indexes.Stores[name];
            var fieldValue = value?.ToString();
            var ids = await index.GetOrAdd(fieldValue, _ => ImmutableHashSet<TId>.Empty);
            return await Task.WhenAll(Enumerable.Select(ids, id => records.Get(id))
                .ToArray());
        }

        public async Task RemoveIndexedValue(TId id, FieldName name, object value)
        {
            var index = indexes.Stores[name];
            var fieldValue = value?.ToString();
            var currentIds = await index.GetOrAdd(fieldValue, _ => ImmutableHashSet<TId>.Empty);
            var ids = currentIds.Remove(id);
            await index.AddOrUpdate(fieldValue, ids, (_, set) => ids);
        }

        public async Task<bool> Remove(TId id)
        {
            (bool success, T record) = await records.TryRemove(id);
            if (!success)
                return false;

            foreach (var kv in indexValueProviders)
            {
                var value = kv.Value(record);
                await RemoveIndexedValue(record.Id, kv.Key, value);
            }

            return true;
        }
    }
}