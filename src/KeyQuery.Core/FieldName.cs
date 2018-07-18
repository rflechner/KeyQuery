using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        Task<(bool, TValue)> TryRemove(TKey key, TValue value);
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

        public Task<(bool, TValue)> TryRemove(TKey key, TValue value)
        {
            var r = memory.TryRemove(key, out var v);
            return Task.FromResult((r,v));
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
            Stores = new ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>>();
        }

        public ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>> Stores { get; }
    }

    public class DataStore<TId, T>
        where T : IDto<TId>
    {
        private IAsyncKeyValueStore<TId, T> records;
        private IndexStore<TId> indexes;
        private IDictionary<string, Func<T, string>> indexValueProviders;

        public DataStore(IAsyncKeyValueStore<TId, T> records, IndexStore<TId> indexes, IDictionary<string, Func<T, string>> indexValueProviders)
        {
            this.records = records;
            this.indexes = indexes;
            this.indexValueProviders = indexValueProviders;
        }

        public static async Task<DataStore<TId, T>> Build(
            Func<IAsyncKeyValueStore<TId,T>> buildPersistence,
            Func<string, Task<IAsyncKeyValueStore<FieldValue, HashSet<TId>>>> buildFieldIndexPersistence,
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


    }

}
