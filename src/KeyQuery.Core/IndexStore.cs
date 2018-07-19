using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace KeyQuery.Core
{
    public class IndexStore<TId>
    {
        public IndexStore()
        {
            Stores = new ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, ImmutableHashSet<TId>>>();
        }

        public ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, ImmutableHashSet<TId>>> Stores { get; }
    }
}