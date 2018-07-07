using System;
using System.Threading.Tasks;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.ReliableCollections;
using Xunit;

namespace KeyQuery.CSharpTests
{
  public class ServiceFabricStorageTests
  {
    [Fact]
    public async Task Storing_value()
    {
      var dictionary = new MockReliableDictionary<Guid, MyDto>(new Uri("fabric://popo"));
      
      var dto = new MyDto(Guid.NewGuid(), "toto", "jean", 32, new DateTime(1985, 02, 11));
      var tx = new MockTransaction(null, 1);
      await dictionary.AddAsync(tx, dto.Id, dto);
    }
  }
}