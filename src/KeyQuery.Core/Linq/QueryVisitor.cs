using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using KeyQuery.Core.Querying;

namespace KeyQuery.Core.Linq
{
  public class QueryVisitor : ExpressionVisitor
  {
    public Operation Operation { get; private set; }
    
    public override Expression Visit(Expression node)
    {
      return base.Visit(node);
    }

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

  public class WhereVisitor : ExpressionVisitor
  {
    public Operation Operation { get; private set; }

    public override Expression Visit(Expression node)
    {
      return base.Visit(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
      return base.VisitMember(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
      if (node.NodeType == ExpressionType.Equal)
        return VisitComparison(node);
      
      if (node.NodeType == ExpressionType.AndAlso){}
        return VisitAnd(node);
      
      return base.VisitBinary(node);
    }

    private Expression VisitAnd(BinaryExpression node)
    {
      // operator &
      var left = new QueryVisitor();
      left.Visit(node.Left);
      var right = new QueryVisitor();
      right.Visit(node.Right);
      
      Operation = new And(left.Operation, right.Operation);

      return node;
    }

    private Expression VisitComparison(BinaryExpression node)
    {
      var path1 = GetPath(node.Left);
      var exp = node.Right;
      
      var value = GetValue(exp);
      Operation = new QueryByField(path1, value);
      
      return node;
    }

    private object GetValue(Expression exp)
    {
      switch (exp)
      {
        case ConstantExpression c:
          return c.Value;
        case MemberExpression m:
          var v1 = Expression.Lambda(m).Compile().DynamicInvoke();
          return v1;
        case UnaryExpression u:
          if (u.Operand is ConstantExpression c2)
            return c2.Value;
          break;
      }
      
      throw new NotImplementedException(exp.ToString());
    }

    string GetPath(Expression expression)
    {
      var propertyVisitor = new PropertyVisitor();
      propertyVisitor.Visit(expression);
      return propertyVisitor.Path;
    }
  }

  public class ComparisonVisitor : ExpressionVisitor
  {
    List<string> paths = new List<string>();
    
    protected override Expression VisitMember(MemberExpression node)
    {
      if (node.Member is PropertyInfo prop)
      {
        var propertyVisitor = new PropertyVisitor();
        propertyVisitor.Visit(node);
        paths.Add(propertyVisitor.Path);
      }
      
      return base.VisitMember(node);
    }
  }
}