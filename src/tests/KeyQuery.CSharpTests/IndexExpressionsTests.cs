using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using NFluent;
using Xunit;

namespace KeyQuery.CSharpTests
{
  public class IndexExpressionsTests
  {
    
    [Fact]
    public void ExpressionMember_should_give_index_path()
    {
      Check.That(Visitors.getIndexName<MyDto, string>(r => r.FirstName)).Equals("FirstName");
      Check.That(Visitors.getIndexName<MyDto, int>(r => r.Birth.Day)).Equals("Birth.Day");
      Check.That(Visitors.getIndexName<MyDto, string>(r => r.Birth.Day.ToString())).Equals("Birth.Day");
    }

    [Fact]
    public void ExpressionMember_should_give_index_value()
    {
      var dto = new MyDto(Guid.NewGuid(), "toto", "jean", 32, new DateTime(1985, 02, 11));
      Expression<Func<MyDto, string>> expression1 = v => v.FirstName;
      Expression<Func<MyDto, string>> expression2 = v => v.Birth.ToString();
      Expression expression3 = expression2;
      
      var f1 = expression1.Compile();
      var f2 = expression2.Compile();
      var value1 = f1(dto);
      var value2 = f2(dto);

      Check.That(value1).Equals("toto");
      Check.That(value2).IsEqualTo(new DateTime(1985, 02, 11).ToString());
    }

    [Fact]
    public async Task InsertRecord_should_index_members()
    {
      var store = DataStore<Guid, MyDto>.Build(
        () => new InMemoryKeyValueStore<Guid, MyDto>(), 
        async _ => new InMemoryKeyValueStore<string, FSharpSet<Guid>>(), 
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