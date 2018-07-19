using System;
using System.Linq.Expressions;

namespace KeyQuery.Core
{
    public static class ExpressionHelpers
    {
        public static string GetIndexName<T, TR>(this Expression<Func<T, TR>> expression)
        {
            var visitor = new PropertyVisitor();
            visitor.Visit(expression);

            return visitor.Path;
        }
    }
}