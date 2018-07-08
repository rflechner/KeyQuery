using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using NFluent;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.ReliableCollections;
using Xunit;

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
      
      var store = DataStore<Guid, MyDto>.Build(
        () => new ServiceFabAsyncKeyValueStore<Guid, MyDto>(wrappedReliableDictionary, transactionBuilder), 
        () => new ServiceFabAsyncKeyValueStore<string, FSharpSet<Guid>>(new MockReliableDictionary<string, FSharpSet<Guid>>(new Uri("fabric://popo")), transactionBuilder),
        new Expression<Func<MyDto, string>>[]
        {
          dto => dto.FirstName,
          dto => dto.Lastname,
          dto => dto.Birth.Day.ToString()
        });

      for (var i = 0; i < 10; i++)
      {
        var d = 1000 + i * 200;
        var dto = new MyDto(Guid.NewGuid(), $"firstname {i}", $"lastname {i}", i, new DateTime(1985, 02, 11) - TimeSpan.FromDays(d));
        await store.Insert(dto);
      }

      var someId = (await store.Records.AllKeys()).First();
      var resultsById = (await OperationModule.execute(store, Operation.NewQueryById(someId))).ToList();
      var expectedById = (await store.Records.AllValues()).First();
      
      Check.That(resultsById).CountIs(1);
      Check.That(resultsById.Single().FirstName).IsEqualTo(expectedById.FirstName);
      Check.That(resultsById.Single().Lastname).IsEqualTo(expectedById.Lastname);
      Check.That(resultsById.Single().Score).IsEqualTo(expectedById.Score);

      var expectedByAndoperation =
        (await OperationModule.execute(store,
            Operation.NewAnd(
              Operation.NewQueryByField("FirstName", "firstname 5"),
              Operation.NewQueryByField("Lastname", "lastname 5")))).ToList();

      Check.That(expectedByAndoperation).CountIs(1);
      Check.That(expectedByAndoperation.Single().FirstName).IsEqualTo("firstname 5");

      var expectedByOroperation =
        (await OperationModule.execute(store,
          Operation.NewOr(
            Operation.NewQueryByField("FirstName", "firstname 5"),
            Operation.NewQueryByField("Lastname", "lastname 4"))))
          .OrderBy(r => r.Score)
          .ToList();
      
      Check.That(expectedByOroperation).CountIs(2);
      Check.That(expectedByOroperation[0].FirstName).IsEqualTo("firstname 4");
      Check.That(expectedByOroperation[1].FirstName).IsEqualTo("firstname 5");
      
      var expectedByOroperation2 =
        (await OperationModule.execute(store,
            Operation.NewOr(
                Operation.NewQueryByField("FirstName", "firstname 5"),
                Operation.NewOr(
                    Operation.NewQueryByField("Lastname", "lastname 4"),
                    Operation.NewQueryByField("Lastname", "lastname 2")
                  )
              )
            ))
          .OrderBy(r => r.Score)
          .ToList();
      
      Check.That(expectedByOroperation2).CountIs(3);
      Check.That(expectedByOroperation2[0].FirstName).IsEqualTo("firstname 2");
      Check.That(expectedByOroperation2[1].FirstName).IsEqualTo("firstname 4");
      Check.That(expectedByOroperation2[2].FirstName).IsEqualTo("firstname 5");
    }
    
  }
}