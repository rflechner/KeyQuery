using System.Collections.Concurrent;
using System.Collections.Generic;

namespace KeyQuery.Core
{
    public class IndexStore<TId>
    {
        public IndexStore()
        {
            Stores = new ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>>();
        }

        public ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, HashSet<TId>>> Stores { get; }
    }
}