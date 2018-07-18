using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        public static string GetIndexName<T, TR>(Expression<Func<T, TR>> expression)
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

    //type IndexStore<'tid when 'tid : comparison> = ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, (Set<'tid>)>>

    public class IndexStore<TId>
    {
        private readonly ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>> stores;

        public IndexStore()
        {
            stores = new ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>>();
        }

        public ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>> Stores => stores;
    }



}
