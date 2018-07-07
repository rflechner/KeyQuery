using System;

namespace KeyQuery.CSharpTests
{
  class MyDto : IDto<Guid>
  {
    public MyDto(Guid id, string firstName, string lastname, int score, DateTime birth)
    {
      Id = id;
      FirstName = firstName;
      Lastname = lastname;
      Score = score;
      Birth = birth;
    }

    public Guid Id { get; }
    public string FirstName { get; }
    public string Lastname { get; }
    public int Score { get; }
    public DateTime Birth { get; }
  }
}