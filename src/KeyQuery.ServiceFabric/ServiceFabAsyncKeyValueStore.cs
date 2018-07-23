using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using KeyQuery.Core;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

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

        private bool IsTransactionnel => Transaction.Current != null;

        private async Task<T> Do<T>(Func<ITransaction, Task<T>> it)
        {
            var tx = transactionBuilder();
            
            try
            {
                var v = await it(tx);

                if (IsTransactionnel)
                    Transaction.Current.TransactionCompleted += async (sender, args) =>
                    {
                        if (tx != null)
                        {
                            await tx.CommitAsync();
                            tx.Dispose();
                        }
                    };
                else
                    await tx.CommitAsync();

                return v;
            }
            finally
            {
                if (!IsTransactionnel)
                    tx.Dispose();
            }
        }
        
        public Task<TValue> AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> updateWith)
        {
            return Do(async tx => await wrappedReliableDictionary.AddOrUpdateAsync(tx, key, value, updateWith));
        }

        public Task<TValue> GetOrAdd(TKey key, Func<TKey, TValue> addValueFactory)
        {
            return Do(async tx => await wrappedReliableDictionary.GetOrAddAsync(tx, key, key1 => addValueFactory.Invoke(key)));
        }

        public Task<bool> TryAdd(TKey key, TValue value)
        {
            return Do(async tx => await wrappedReliableDictionary.TryAddAsync(tx, key, value));
        }

        public Task<(bool success, TValue value)> TryRemove(TKey key)
        {
            return Do(async tx =>
            {
                var v = await wrappedReliableDictionary.TryRemoveAsync(tx, key);
                return v.HasValue ? (true, v.Value) : (false, default(TValue));
            });
        }

        public Task<TValue> Get(TKey key)
        {
            return Do(async tx =>
            {
                var conditionalValue = await wrappedReliableDictionary.TryGetValueAsync(tx, key);
                return conditionalValue.HasValue ? conditionalValue.Value : default(TValue);
            });
        }

        public async Task<ICollection<TKey>> AllKeys()
        {
            return await Do(async tx =>
            {
                var e = await wrappedReliableDictionary.CreateEnumerableAsync(tx);
                var enumerator = e.GetAsyncEnumerator();

                var results = new List<TKey>();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    results.Add(enumerator.Current.Key);
                }

                return results.AsReadOnly();
            });
        }

        public async Task<ICollection<TValue>> AllValues()
        {
            return await Do(async tx =>
            {
                var e = await wrappedReliableDictionary.CreateEnumerableAsync(tx);
                var enumerator = e.GetAsyncEnumerator();

                var results = new List<TValue>();

                while (await enumerator.MoveNextAsync(CancellationToken.None))
                {
                    results.Add(enumerator.Current.Value);
                }

                return results.AsReadOnly();
            });
        }
    }
}