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
      if (Convert(node, out var operation))
      {
        Operation = operation;
        return node;
      }

      return base.VisitBinary(node);
    }

    static bool Convert(BinaryExpression node, out Operation operation)
    {
      switch (node.NodeType)
      {
        case ExpressionType.Equal:
          operation = VisitComparison(node);
          return true;
        case ExpressionType.AndAlso:
          operation = VisitAnd(node);
          return true;
        case ExpressionType.OrElse:
          operation = VisitOr(node);
          return true;
      }

      operation = null;

      return false;
    }

    private static Operation VisitBinaryOperation(BinaryExpression node, Func<Operation, Operation, Operation> builder)
    {
      if (!(node.Left is BinaryExpression leftExpression) || !Convert(leftExpression, out var left))
        throw new NotSupportedException();
      
      if (!(node.Right is BinaryExpression rightExpression) || !Convert(rightExpression, out var right))
        throw new NotSupportedException();

      return builder(left, right);
    }
    
    private static Operation VisitAnd(BinaryExpression node) => VisitBinaryOperation(node, (left, right) => new And(left, right));

    private static Operation VisitOr(BinaryExpression node) => VisitBinaryOperation(node, (left, right) => new Or(left, right));

    private static Operation VisitComparison(BinaryExpression node)
    {
      var path1 = GetPath(node.Left);
      var exp = node.Right;
      
      var value = GetValue(exp);
      return new QueryByField(path1, value);
    }

    private static object GetValue(Expression exp)
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

    static string GetPath(Expression expression)
    {
      var propertyVisitor = new PropertyVisitor();
      propertyVisitor.Visit(expression);
      return propertyVisitor.Path;
    }
  }

  public class ComparisonVisitor : ExpressionVisitor
  {
    private readonly List<string> paths = new List<string>();
    
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