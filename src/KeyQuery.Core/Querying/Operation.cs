using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace KeyQuery.Core.Querying
{
    public abstract class Operation
    {
        public abstract Task<ICollection<T>> Execute<TId, T>(DataStore<TId, T> store) where T : IDto<TId>;
    }
}
