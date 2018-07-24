using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace WebSample1.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly IReliableStateManager stateManager;
        private readonly DataStore<Guid, Customer> store;

        public CustomerRepository(IReliableStateManager stateManager, DataStore<Guid, Customer> store)
        {
            this.stateManager = stateManager;
            this.store = store;
        }

        public async Task<Customer> Get(Guid id)
        {
            return await store.FindOne(x => x.Where(c => c.Id == id));
        }

        public async Task<bool> Insert(Customer customer)
        {
            using (var tx = new TransactionScope())
            {
                var r = await store.Insert(customer);

                tx.Complete();

                return r;
            }
        }

        public async Task<ICollection<Customer>> GetAllCustomers(CancellationToken cancellationToken = default)
        {
            var customers = await stateManager.GetOrAddAsync<IReliableDictionary<Guid, Customer>>("Customer");

            var results = new List<Customer>();

            using (var tx = stateManager.CreateTransaction())
            {
                var enumerable = await customers.CreateEnumerableAsync(tx);
                var enumerator = enumerable.GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(cancellationToken) && !cancellationToken.IsCancellationRequested)
                {
                    results.Add(enumerator.Current.Value);
                }
            }

            return results;
        }
    }
}