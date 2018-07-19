using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace KeyQuery.Core
{
    public class FieldName
    {
        public string Value { get; }

        public FieldName(string value)
        {
            Value = value;
        }

        public static implicit operator string(FieldName name) => name.Value;

        public static implicit operator FieldName(string name) => new FieldName(name);

        public override string ToString() => Value;
    }

    public class FieldValue
    {
        public string Value { get; }

        public FieldValue(string value)
        {
            Value = value;
        }

        public static implicit operator string(FieldValue name) => name.Value;

        public static implicit operator FieldValue(string name) => new FieldValue(name);

        public override string ToString() => Value;
    }

    public class PropertyVisitor : ExpressionVisitor
    {
        private readonly List<string> names = new List<string>();

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo prop)
                names.Add(prop.Name);
            return base.VisitMember(node);
        }

        public string Path => string.Join(".", names);
    }

    public static class ExpressionHelpers
    {
        public static string GetIndexName<T, TR>(this Expression<Func<T, TR>> expression)
        {
            var visitor = new PropertyVisitor();
            visitor.Visit(expression);

            return visitor.Path;
        }
    }

    public interface IDto<TId>
    {
        TId Id { get; }
    }

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

    public class InMemoryKeyValueStore<TKey, TValue> : IAsyncKeyValueStore<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> memory = new ConcurrentDictionary<TKey, TValue>();

        public Task<TValue> AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> updateWith)
            => Task.FromResult(memory.AddOrUpdate(key, value, updateWith));

        public Task<TValue> GetOrAdd(TKey key, Func<TKey, TValue> addWith)
            => Task.FromResult(memory.GetOrAdd(key, addWith));

        public Task<bool> TryAdd(TKey key, TValue value)
            => Task.FromResult(memory.TryAdd(key, value));

        public Task<(bool success, TValue value)> TryRemove(TKey key)
        {
            var r = memory.TryRemove(key, out var v);
            return Task.FromResult((r, v));
        }

        public Task<TValue> Get(TKey key)
            => Task.FromResult(memory[key]);

        public Task<ICollection<TKey>> AllKeys()
            => Task.FromResult(memory.Keys);

        public Task<ICollection<TValue>> AllValues()
            => Task.FromResult(memory.Values);
    }

    public class IndexStore<TId>
    {
        public IndexStore()
        {
            Stores = new ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, ImmutableHashSet<TId>>>();
        }

        public ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, ImmutableHashSet<TId>>> Stores { get; }
    }

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

            return new DataStore<TId, T>(buildPersistence(), indexes, indexedFields.ToDictionary(kv => kv.Item1, kv => kv.Item2));
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
            return await Task.WhenAll(ids
                    .Select(id => records.Get(id))
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
