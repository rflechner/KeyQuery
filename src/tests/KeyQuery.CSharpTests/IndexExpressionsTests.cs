using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
    }

    [Fact]
    public void ExpressionMember_should_give_index_value()
    {
      var dto = new MyDto(Guid.NewGuid(), "toto", "jean", 32, new DateTime(1985, 02, 11));
      Expression<Func<MyDto, string>> expression = v => v.FirstName;
      var f = expression.Compile();
      var value = f(dto);

      Check.That(value).Equals("toto");
    }
    
  }
}