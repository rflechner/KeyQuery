using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KeyQuery.Core.Linq.Visitors;

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

        public async Task<bool> Insert(T record)
        {
            using (var tx = records.CreateTransaction())
            {
                if (!await records.TryAdd(tx, record.Id, record))
                    return false;

                foreach (var index in indexValueProviders)
                {
                    var value = index.Value(record);
                    await Index(tx, record.Id, index.Key, value);
                }

                await tx.CommitAsync();
                return true;
            }
        }

        private async Task Index(ITransaction tx, TId id, FieldName name, object value)
        {
            var index = indexes.Stores[name];
            var fieldValue = value?.ToString();
            var currentIds = await index.GetOrAdd(tx, fieldValue, _ => new HashSet<TId>());
            var newIds = new HashSet<TId>(currentIds) {id};
            await index.AddOrUpdate(tx, fieldValue, newIds, (_, __) => newIds);
        }

        public async Task<ICollection<T>> SearchByIndex(FieldName name, object value)
        {
            using (var tx = records.CreateTransaction())
            {
                var index = indexes.Stores[name];
                var fieldValue = value?.ToString();
                var ids = await index.GetOrAdd(tx, fieldValue, _ => new HashSet<TId>());
                var results = await Task.WhenAll(ids.Select(id => records.Get(tx, id)).ToArray());

                await tx.CommitAsync();
                
                return results;
            }
        }

        private async Task RemoveIndexedValue(ITransaction tx, TId id, FieldName name, object value)
        {
            var index = indexes.Stores[name];
            var fieldValue = value?.ToString();
            var currentIds = await index.GetOrAdd(tx, fieldValue, _ => new HashSet<TId>());
            var newIds = new HashSet<TId>(currentIds);
            newIds.Remove(id);
            await index.AddOrUpdate(tx, fieldValue, newIds, (_, __) => newIds);
        }

        public async Task<bool> Remove(TId id)
        {
            using (var tx = records.CreateTransaction())
            {
                (bool success, T record) = await records.TryRemove(tx, id);
                if (!success)
                    return false;

                foreach (var kv in indexValueProviders)
                {
                    var value = kv.Value(record);
                    await RemoveIndexedValue(tx, record.Id, kv.Key, value);
                }
                await tx.CommitAsync();
                
                return true;
            }
        }

        public async Task<T> FindOne(Func<IQueryable<T>, IQueryable<T>> query)
        {
            return (await Find(query)).SingleOrDefault();
        }
        
        public Task<ICollection<T>> Find(Func<IQueryable<T>, IQueryable<T>> query)
        {
            var queryable = Enumerable.Empty<T>().AsQueryable();
            return Find(query(queryable));
        }
        
        public async Task<ICollection<T>> Find(IQueryable<T> query)
        {
            var expression = query.Expression;
            var visitor = new QueryVisitor();
            visitor.Visit(expression);
            return await visitor.Operation.Execute(this);
        }
    }
}