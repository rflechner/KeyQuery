using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KeyQuery;
using KeyQuery.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using WebSample1.Repositories;

namespace WebSample1.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        private readonly ICustomerRepository customerRepository;

        public ValuesController(ICustomerRepository customerRepository)
        {
            this.customerRepository = customerRepository;
        }

        // GET api/values
        [HttpGet]
        public Task<ICollection<Customer>> Get(CancellationToken cancellationToken)
        {
            return customerRepository.GetAllCustomers(cancellationToken);
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var customer = await customerRepository.Get(id);
            if (customer == null)
                return StatusCode((int) HttpStatusCode.NotFound);
            return Json(customer);
        }

        // POST api/values
        [HttpPost]
        public async Task Post([FromBody]string value)
        {
            var id = Guid.NewGuid();
            var dto = new Customer(id, value, $"Lastname {DateTime.Now.Ticks}",
                DateTime.Now.Millisecond, new DateTime(1985, 02, 11));

            await customerRepository.Insert(dto);
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
