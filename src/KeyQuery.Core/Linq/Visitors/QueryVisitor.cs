using System.Linq.Expressions;
using KeyQuery.Core.Querying;

namespace KeyQuery.Core.Linq.Visitors
{
  public class QueryVisitor : ExpressionVisitor
  {
    public Operation Operation { get; private set; }
    
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
      if (node.Method.Name == "Where")
      {
        var whereVisitor = new WhereVisitor();
        whereVisitor.Visit(node);
        Operation = whereVisitor.Operation;
      }
      
      return base.VisitMethodCall(node);
    }
  }
}