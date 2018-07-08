﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
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

    public async Task<TValue> AddOrUpdate(TKey key, TValue value, FSharpFunc<TKey, FSharpFunc<TValue, TValue>> updateValueFactory)
    {
      using (var tx = transactionBuilder())
      {
        var v = await wrappedReliableDictionary.AddOrUpdateAsync(tx, key, value, (key1, value1) => updateValueFactory.Invoke(key1).Invoke(value1));
        await tx.CommitAsync();

        return v;
      }
    }

    public async Task<TValue> GetOrAdd(TKey key, FSharpFunc<TKey, TValue> addValueFactory)
    {
      using (var tx = transactionBuilder())
      {
        var v = await wrappedReliableDictionary.GetOrAddAsync(tx, key, key1 => addValueFactory.Invoke(key));
        await tx.CommitAsync();

        return v;
      }
    }

    public async Task<bool> TryAdd(TKey key, TValue value)
    {
      using (var tx = transactionBuilder())
      {
        var v = await wrappedReliableDictionary.TryAddAsync(tx, key, value);
        await tx.CommitAsync();

        return v;
      }
    }

    public async Task<Tuple<bool, TValue>> TryRemove(TKey key)
    {
      using (var tx = transactionBuilder())
      {
        var v = await wrappedReliableDictionary.TryRemoveAsync(tx, key);
        await tx.CommitAsync();
        
        return v.HasValue ? new Tuple<bool, TValue>(true, v.Value) : new Tuple<bool, TValue>(false, default(TValue));
      }
    }

    public async Task<TValue> Get(TKey key)
    {
      using (var tx = transactionBuilder())
      {
        var conditionalValue = await wrappedReliableDictionary.TryGetValueAsync(tx, key);
        
        return conditionalValue.HasValue ? conditionalValue.Value : default(TValue);
      }
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