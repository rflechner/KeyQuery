using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using KeyQuery.Core;
using KeyQuery.Core.Querying;
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
    public class ServiceFabricStorageTests
    {
        [Fact]
        public async Task Storing_value()
        {
            var dictionary = new MockReliableDictionary<Guid, MyDto>(new Uri("fabric://popo"));

            var dto = new MyDto(Guid.NewGuid(), "toto", "jean", 32, new DateTime(1985, 02, 11));
            var tx = new MockTransaction(null, 1);
            await dictionary.AddAsync(tx, dto.Id, dto);
        }

        [Fact]
        public async Task InsertRecord_should_index_members()
        {
            IReliableDictionary<Guid, MyDto> wrappedReliableDictionary = new MockReliableDictionary<Guid, MyDto>(new Uri("fabric://popo"));
            Func<ITransaction> transactionBuilder = () => new MockTransaction(null, 1);

            var store = await DataStore<Guid, MyDto>.Build(
              () => new ServiceFabAsyncKeyValueStore<Guid, MyDto>(wrappedReliableDictionary, transactionBuilder),
              async _ => new ServiceFabAsyncKeyValueStore<FieldValue, HashSet<Guid>>(new MockReliableDictionary<FieldValue, HashSet<Guid>>(new Uri("fabric://popo")), transactionBuilder),
              new Expression<Func<MyDto, string>>[]
              {
                  dto => dto.FirstName,
                  dto => dto.Lastname,
                  dto => dto.Birth.Day.ToString()
              });

            for (var i = 0; i < 10; i++)
            {
                var d = 1000 + i * 200;
                var dto = new MyDto(Guid.NewGuid(), $"firstname {i}", $"lastname {i}", i,
                    new DateTime(1985, 02, 11) - TimeSpan.FromDays(d));
                await store.Insert(dto);
            }

            var someId = (await store.Records.AllKeys()).First();
            var resultsById = (await new QueryById(someId).Execute(store)).ToList();
            var expectedById = (await store.Records.AllValues()).First();

            Check.That(resultsById).CountIs(1);
            Check.That(resultsById.Single().FirstName).IsEqualTo(expectedById.FirstName);
            Check.That(resultsById.Single().Lastname).IsEqualTo(expectedById.Lastname);
            Check.That(resultsById.Single().Score).IsEqualTo(expectedById.Score);

            var expectedByAndoperation =
              (await
                  new And(
                    new QueryByField("FirstName", "firstname 5"),
                    new QueryByField("Lastname", "lastname 5")).Execute(store)).ToList();

            Check.That(expectedByAndoperation).CountIs(1);
            Check.That(expectedByAndoperation.Single().FirstName).IsEqualTo("firstname 5");

            var expectedByOroperation =
              (await
                new Or(
                  new QueryByField("FirstName", "firstname 5"),
                  new QueryByField("Lastname", "lastname 4")).Execute(store))
                .OrderBy(r => r.Score)
                .ToList();

            Check.That(expectedByOroperation).CountIs(2);
            Check.That(expectedByOroperation[0].FirstName).IsEqualTo("firstname 4");
            Check.That(expectedByOroperation[1].FirstName).IsEqualTo("firstname 5");

            var expectedByOroperation2 =
              (await
                  new Or(
                      new QueryByField("FirstName", "firstname 5"),
                      new Or(
                          new QueryByField("Lastname", "lastname 4"),
                          new QueryByField("Lastname", "lastname 2")
                        )).Execute(store))
                .OrderBy(r => r.Score)
                .ToList();

            Check.That(expectedByOroperation2).CountIs(3);
            Check.That(expectedByOroperation2[0].FirstName).IsEqualTo("firstname 2");
            Check.That(expectedByOroperation2[1].FirstName).IsEqualTo("firstname 4");
            Check.That(expectedByOroperation2[2].FirstName).IsEqualTo("firstname 5");

            var myDtos = await store.Find(q => q.Where(m => m.Lastname == "lastname 4" || m.FirstName == "firstname 2"));

            Check.That(myDtos).CountIs(2);
        }

    }
}