using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KeyQuery.Core.Querying
{
    public class Or : Operation
    {
        public Or(Operation first, Operation second)
        {
            First = first;
            Second = second;
        }

        public Operation First { get; }

        public Operation Second { get; }


        public override async Task<ICollection<T>> Execute<TId, T>(DataStore<TId, T> store)
        {
            var r1 = await First.Execute(store);
            var r2 = await Second.Execute(store);

            return r1.Concat(r2).Distinct().ToArray();
        }
    }
}