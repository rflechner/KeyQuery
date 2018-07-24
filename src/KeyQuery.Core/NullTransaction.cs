using System.Threading.Tasks;

namespace KeyQuery.Core
{
  public class NullTransaction : ITransaction
  {
    public void Dispose()
    {
    }

    public long Id => 0;
        
    public Task Abort() => Task.CompletedTask;

    public Task CommitAsync() => Task.CompletedTask;
  }
}