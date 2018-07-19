using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace KeyQuery.Core
{
    public class PropertyVisitor : ExpressionVisitor
    {
        private readonly List<string> names = new List<string>();

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo prop)
                names.Insert(0, prop.Name);
            return base.VisitMember(node);
        }

        public string Path => string.Join(".", names);
    }
}