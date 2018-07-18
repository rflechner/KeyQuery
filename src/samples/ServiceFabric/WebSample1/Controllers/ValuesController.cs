using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KeyQuery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace WebSample1.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly IReliableStateManager stateManager;
        private readonly DataStore<Guid, Customer> store;

        public ValuesController(IReliableStateManager stateManager, DataStore<Guid, Customer> store)
        {
            this.stateManager = stateManager;
            this.store = store;
        }

        // GET api/values
        [HttpGet]
        public async Task<ICollection<Customer>> Get(CancellationToken cancellationToken)
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

        // GET api/values/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var customers = await stateManager.GetOrAddAsync<IReliableDictionary<Guid, Customer>>("Customer");

            using (var tx = stateManager.CreateTransaction())
            {
                var result = await customers.TryGetValueAsync(tx, id);
                if (result.HasValue)
                    return Json(result.Value);
                
                return StatusCode((int) HttpStatusCode.NotFound);
            }
        }

        // POST api/values
        [HttpPost]
        public async Task Post([FromBody]string value)
        {
            var id = Guid.NewGuid();
            var dto = new Customer(id, value, $"Lastname {DateTime.Now.Ticks}",
                DateTime.Now.Millisecond, new DateTime(1985, 02, 11));

            await store.Insert(dto);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
