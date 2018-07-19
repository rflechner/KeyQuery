using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyQuery.Core.Querying
{
    public class QueryByField : Operation
    {
        public QueryByField(FieldName name, object fieldValue)
        {
            Name = name;
            FieldValue = fieldValue;
        }

        public FieldName Name { get; }

        public object FieldValue { get; }

        public override async Task<ICollection<T>> Execute<TId, T>(DataStore<TId, T> store)
        {
            return await store.SearchByIndex(Name, FieldValue);
        }
    }
}