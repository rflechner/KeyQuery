using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebSample1.Repositories
{
    public interface ICustomerRepository
    {
        Task<ICollection<Customer>> GetAllCustomers(CancellationToken cancellationToken = default);
        Task<Customer> Get(Guid id);
        Task<bool> Insert(Customer customer);
        Task<ICollection<Customer>> SearchByFirstname(string firstname);
    }
}