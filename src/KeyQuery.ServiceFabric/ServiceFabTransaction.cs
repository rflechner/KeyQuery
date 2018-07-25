using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;

namespace KeyQuery.ServiceFabric
{
  public class ServiceFabTransaction : KeyQuery.Core.ITransaction
  {
    private readonly ITransaction wrappedTransaction;

    public ServiceFabTransaction(ITransaction wrappedTransaction)
    {
      this.wrappedTransaction = wrappedTransaction;
    }

    public long Id => wrappedTransaction.TransactionId;

    public Task Abort()
    {
      wrappedTransaction.Abort();
      return Task.CompletedTask;
    }

    public Task CommitAsync()
    {
      return wrappedTransaction.CommitAsync();
    }

    public void Dispose()
    {
      wrappedTransaction.Dispose();
    }

    public ITransaction WrappedTransaction => wrappedTransaction;
  }
}