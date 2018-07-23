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

namespace KeyQuery.CSharpTests
{
    public class TransactionsTests
    {
        [Fact]
        public async Task WhenUsing_transactionScope_SF_transations_should_commit_on_complete()
        {
            Func<ITransaction> transactionBuilder = () => new MockTransaction(null, 1);
            var wrappedReliableDictionary = new MockReliableDictionary<Guid, MyDto>(new Uri("fabric://popo"));
            var store = await DataStore<Guid, MyDto>.Build(
                () => new ServiceFabAsyncKeyValueStore<Guid, MyDto>(wrappedReliableDictionary, transactionBuilder),
                async _ => new ServiceFabAsyncKeyValueStore<FieldValue, HashSet<Guid>>(new MockReliableDictionary<FieldValue, HashSet<Guid>>(new Uri("fabric://popo")), transactionBuilder),
                new Expression<Func<MyDto, string>>[]
                {
                    dto => dto.FirstName,
                    dto => dto.Lastname,
                    dto => dto.Birth.Day.ToString()
                });

            using (var scope = new TransactionScope())
            {
                for (var i = 0; i < 10; i++)
                {
                    var d = 1000 + i * 200;
                    var dto = new MyDto(Guid.NewGuid(), $"firstname {i}", $"lastname {i}", i,
                        new DateTime(1985, 02, 11) - TimeSpan.FromDays(d));
                    await store.Insert(dto);
                }


                Check.That(wrappedReliableDictionary.Count).IsEqualTo(0);
                    
                
                scope.Complete();
            }


        }

    }
}