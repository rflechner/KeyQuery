using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Transactions;
using KeyQuery.Core;
using KeyQuery.ServiceFabric;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using NFluent;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.ReliableCollections;
using Xunit;
using ITransaction = Microsoft.ServiceFabric.Data.ITransaction;

namespace KeyQuery.CSharpTests
{
    public class TransactionsTests
    {
        [Fact]
        public async Task When_transaction_is_not_completed__then_dto_are_not_committed()
        {
            Func<ITransaction> transactionBuilder = () => new MockTransaction(null, 1);
            var wrappedReliableDictionary = new MockReliableDictionary<Guid, MyDto>(new Uri("fabric://popo"));
            var store = await DataStore<Guid, MyDto>.Build
            (
                () => new ServiceFabAsyncKeyValueStore<Guid, MyDto>(wrappedReliableDictionary, transactionBuilder),
                async _ => new ServiceFabAsyncKeyValueStore<FieldValue, HashSet<Guid>>(new MockReliableDictionary<FieldValue, HashSet<Guid>>(new Uri("fabric://popo")), transactionBuilder), 
                dto => dto.FirstName, 
                dto => dto.Lastname, 
                dto => dto.Birth.Day.ToString()
            );
            
            using (var tx = store.CreateTransaction())
            {
                for (var i = 0; i < 1; i++)
                {
                    var d = 1000 + i * 200;
                    var dto = new MyDto(Guid.NewGuid(), $"firstname {i}", $"lastname {i}", i,
                        new DateTime(1985, 02, 11) - TimeSpan.FromDays(d));
                    await store.Insert(tx, dto);
                }
            }
            
            Check.That(wrappedReliableDictionary.Count).IsEqualTo(0);
        }
        
        [Fact]
        public async Task When_transaction_is_completed__then_dto_are_committed()
        {
            Func<ITransaction> transactionBuilder = () => new MockTransaction(null, 1);
            var wrappedReliableDictionary = new MockReliableDictionary<Guid, MyDto>(new Uri("fabric://popo"));
            var store = await DataStore<Guid, MyDto>.Build
            (
                () => new ServiceFabAsyncKeyValueStore<Guid, MyDto>(wrappedReliableDictionary, transactionBuilder),
                async _ => new ServiceFabAsyncKeyValueStore<FieldValue, HashSet<Guid>>(new MockReliableDictionary<FieldValue, HashSet<Guid>>(new Uri("fabric://popo")), transactionBuilder), 
                dto => dto.FirstName, 
                dto => dto.Lastname, 
                dto => dto.Birth.Day.ToString()
            );

            var count = 100;
            
            using (var tx = store.CreateTransaction())
            {
                for (var i = 0; i < count; i++)
                {
                    var d = 1000 + i * 200;
                    var dto = new MyDto(Guid.NewGuid(), $"firstname {i}", $"lastname {i}", i,
                        new DateTime(1985, 02, 11) - TimeSpan.FromDays(d));
                    await store.Insert(tx, dto);
                }
               
                await tx.CommitAsync();
            }
            
            Check.That(wrappedReliableDictionary.Count).IsEqualTo(count);
        }
    }
}