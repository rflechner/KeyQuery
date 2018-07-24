using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KeyQuery.Core;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using ITransaction = Microsoft.ServiceFabric.Data.ITransaction;

namespace KeyQuery.ServiceFabric
{
    public static class StoreBuilder
    {
        public static async Task<DataStore<TId, TModel>> AddDocumentStore<TId, TModel>(
            this IReliableStateManager stateManager, Expression<Func<TModel, string>>[] indexedMembers, string name = null)
          where TId : IComparable<TId>, IEquatable<TId>
          where TModel : IDto<TId>
        {
            if (string.IsNullOrWhiteSpace(name))
                name = typeof(TModel).Name;

            var wrappedReliableDictionary = await stateManager.GetOrAddAsync<IReliableDictionary<TId, TModel>>(name);
            Func<ITransaction> transactionBuilder = stateManager.CreateTransaction;

            async Task<IAsyncKeyValueStore<FieldValue, HashSet<TId>>> BuildFieldIndexPersistence(string memberName)
            {
                var dictionary = await stateManager.GetOrAddAsync<IReliableDictionary<FieldValue, HashSet<TId>>>($"{name}_{memberName}");

                return new ServiceFabAsyncKeyValueStore<FieldValue, HashSet<TId>>(dictionary, transactionBuilder);
            }

            var store = await DataStore<TId, TModel>.Build(
              () => new ServiceFabAsyncKeyValueStore<TId, TModel>(wrappedReliableDictionary, transactionBuilder),
              BuildFieldIndexPersistence,
              indexedMembers);

            return store;
        }
    }
}
