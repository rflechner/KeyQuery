using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using KeyQuery.Core;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using ITransaction = Microsoft.ServiceFabric.Data.ITransaction;

namespace KeyQuery.ServiceFabric
{
    public class ServiceFabAsyncKeyValueStore<TKey, TValue> : IAsyncKeyValueStore<TKey, TValue> where TKey : IComparable<TKey>, IEquatable<TKey>
    {
        private readonly IReliableDictionary<TKey, TValue> wrappedReliableDictionary;
        private readonly Func<ITransaction> transactionBuilder;

        public ServiceFabAsyncKeyValueStore(IReliableDictionary<TKey, TValue> wrappedReliableDictionary, Func<ITransaction> transactionBuilder)
        {
            this.wrappedReliableDictionary = wrappedReliableDictionary;
            this.transactionBuilder = transactionBuilder;
        }
        
        public Core.ITransaction CreateTransaction()
        {
            return new ServiceFabTransaction(transactionBuilder());
        }
        
        public Task<TValue> AddOrUpdate(Core.ITransaction tx, TKey key, TValue value, Func<TKey, TValue, TValue> updateWith)
        {
            return wrappedReliableDictionary.AddOrUpdateAsync(((ServiceFabTransaction)tx).WrappedTransaction, key, value, updateWith);
        }

        public Task<TValue> GetOrAdd(Core.ITransaction tx, TKey key, Func<TKey, TValue> addValueFactory)
        {
            return wrappedReliableDictionary.GetOrAddAsync(((ServiceFabTransaction)tx).WrappedTransaction, key, key1 => addValueFactory.Invoke(key));
        }

        public Task<bool> TryAdd(Core.ITransaction tx, TKey key, TValue value)
        {
            return wrappedReliableDictionary.TryAddAsync(((ServiceFabTransaction)tx).WrappedTransaction, key, value);
        }

        public async Task<(bool success, TValue value)> TryRemove(Core.ITransaction tx, TKey key)
        {
            var v = await wrappedReliableDictionary.TryRemoveAsync(((ServiceFabTransaction)tx).WrappedTransaction, key);
            return v.HasValue ? (true, v.Value) : (false, default(TValue));
        }

        public async Task<TValue> Get(Core.ITransaction tx, TKey key)
        {
            var conditionalValue = await wrappedReliableDictionary.TryGetValueAsync(((ServiceFabTransaction)tx).WrappedTransaction, key);
            return conditionalValue.HasValue ? conditionalValue.Value : default(TValue);
        }

        public async Task<ICollection<TKey>> AllKeys()
        {
            using (var tx = transactionBuilder())
            {
                var e = await wrappedReliableDictionary.CreateEnumerableAsync(tx);
                var enumerator = e.GetAsyncEnumerator();

                var results = new List<TKey>();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    results.Add(enumerator.Current.Key);
                }

                return results.AsReadOnly();
            }
        }

        public async Task<ICollection<TValue>> AllValues()
        {
            using (var tx = transactionBuilder())
            {
                var e = await wrappedReliableDictionary.CreateEnumerableAsync(tx);
                var enumerator = e.GetAsyncEnumerator();

                var results = new List<TValue>();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    results.Add(enumerator.Current.Value);
                }

                return results.AsReadOnly();
            }
        }
    }
}