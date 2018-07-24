using System;
using System.Threading.Tasks;

namespace KeyQuery.Core
{
  public interface ITransaction : IDisposable
  {
    long Id { get; }
    Task Abort();
    Task CommitAsync();
  }
}