using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyQuery.Core.Querying
{
    public class QueryById : Operation
    {
        public QueryById(object id)
        {
            Id = id;
        }

        public object Id { get; }

        public override async Task<ICollection<T>> Execute<TId, T>(DataStore<TId, T> store)
        {
            var record = await store.Records.Get((TId) Id);

            return new[]{record};
        }
    }
}