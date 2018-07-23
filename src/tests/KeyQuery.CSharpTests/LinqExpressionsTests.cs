using System;
using System.Collections.Generic;
using System.Linq;
using KeyQuery.Core;
using KeyQuery.Core.Linq;
using KeyQuery.Core.Linq.Visitors;
using KeyQuery.Core.Querying;
using NFluent;
using Xunit;

namespace KeyQuery.CSharpTests
{
  public class LinqExpressionsTests
  {
    readonly IQueryable<MyDto> collection = new List<MyDto>().AsQueryable();

    [Fact]
    public void Expression_should_give_operationById()
    {
      var queryable = collection.Where(dto => dto.Id == Guid.Empty);

      var visitor = new QueryVisitor();
      visitor.Visit(queryable.Expression);

      Check.That(visitor.Operation).IsInstanceOf<QueryById>();

      var queryById = (QueryById) visitor.Operation;

      Check.That(queryById.Id).IsEqualTo(Guid.Empty);
    }
    
    [Fact]
    public void Expression_should_give_operation_and()
    {
      var queryable = collection.Where(dto => dto.Id == Guid.Empty && dto.FirstName == "lalala");

      var visitor = new QueryVisitor();
      visitor.Visit(queryable.Expression);

      Check.That(visitor.Operation).IsInstanceOf<And>();

      var and = (And) visitor.Operation;
      var first = (QueryById) and.First;
      var second = (QueryByField) and.Second;

      Check.That(first.Id).IsEqualTo(Guid.Empty);
      
      Check.That(second.Name.Value).IsEqualTo("FirstName");
      Check.That(second.FieldValue).IsEqualTo("lalala");
    }
    
    [Fact]
    public void Expression_should_give_operation_or()
    {
      var queryable = collection.Where(dto => dto.Id == Guid.Empty || dto.FirstName == "lalala");

      var visitor = new QueryVisitor();
      visitor.Visit(queryable.Expression);

      Check.That(visitor.Operation).IsInstanceOf<Or>();
      
      var or = (Or) visitor.Operation;
      var first = (QueryById) or.First;
      var second = (QueryByField) or.Second;

      Check.That(first.Id).IsEqualTo(Guid.Empty);
      
      Check.That(second.Name.Value).IsEqualTo("FirstName");
      Check.That(second.FieldValue).IsEqualTo("lalala");
    }
    
    [Fact]
    public void Expression_should_give_operation_and_or()
    {
      var queryable = collection.Where(dto => dto.Id == Guid.Empty && dto.Score == 10 || dto.FirstName == "lalala");

      var visitor = new QueryVisitor();
      visitor.Visit(queryable.Expression);

      Check.That(visitor.Operation).IsInstanceOf<Or>();
      
      var or = (Or) visitor.Operation;
      var and = (And) or.First;
      var first = (QueryById) and.First;
      var second = (QueryByField) and.Second;

      var third = (QueryByField) or.Second;

      Check.That(first.Id).IsEqualTo(Guid.Empty);
      
      Check.That(second.Name.Value).IsEqualTo("Score");
      Check.That(second.FieldValue).IsEqualTo(10);
      
      Check.That(third.Name.Value).IsEqualTo("FirstName");
      Check.That(third.FieldValue).IsEqualTo("lalala");
    }
    
  }
}