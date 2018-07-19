using System;
using System.Collections.Generic;
using System.Linq;
using KeyQuery.Core;
using KeyQuery.Core.Linq;
using KeyQuery.Core.Querying;
using NFluent;
using Xunit;

namespace KeyQuery.CSharpTests
{
  public class LinqExpressionsTests
  {
    
    IQueryable<MyDto> collection = new List<MyDto>().AsQueryable();

    [Fact]
    public void Expression_should_give_operationById()
    {
      var queryable = collection.Where(dto => dto.Id == Guid.Empty);

      var visitor = new QueryVisitor();
      visitor.Visit(queryable.Expression);

      Check.That(visitor.Operation).IsInstanceOf<QueryByField>();      
    }
    
    [Fact]
    public void Expression_should_give_operation_and()
    {
      var queryable = collection.Where(dto => dto.Id == Guid.Empty && dto.FirstName == "lalala");

      var visitor = new QueryVisitor();
      visitor.Visit(queryable.Expression);

      Check.That(visitor.Operation).IsInstanceOf<And>();      
    }
    
  }
}