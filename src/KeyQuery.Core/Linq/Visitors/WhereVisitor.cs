using System;
using System.Linq.Expressions;
using System.Reflection;
using KeyQuery.Core.Querying;

namespace KeyQuery.Core.Linq.Visitors
{
  public class WhereVisitor : ExpressionVisitor
  {
    public Operation Operation { get; private set; }

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
      var value = GetValue(node.Right);
      
      if (node.Left is MemberExpression left && left.Member is PropertyInfo prop)
      {
        if (prop.Name == nameof(IDto<object>.Id))
        {
          return new QueryById(value);
        }
      }
      
      var path1 = GetPath(node.Left);
      
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
      
      throw new NotSupportedException();
    }

    static string GetPath(Expression expression)
    {
      var propertyVisitor = new PropertyVisitor();
      propertyVisitor.Visit(expression);
      return propertyVisitor.Path;
    }
  }
}