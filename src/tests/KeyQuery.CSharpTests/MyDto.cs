using System;

namespace KeyQuery.CSharpTests
{
  class MyDto : IDto<Guid>, IEquatable<MyDto>
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

    public bool Equals(MyDto other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return Id.Equals(other.Id) && string.Equals(FirstName, other.FirstName) && string.Equals(Lastname, other.Lastname) && Score == other.Score && Birth.Equals(other.Birth);
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != this.GetType()) return false;
      return Equals((MyDto) obj);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        var hashCode = Id.GetHashCode();
        hashCode = (hashCode * 397) ^ (FirstName != null ? FirstName.GetHashCode() : 0);
        hashCode = (hashCode * 397) ^ (Lastname != null ? Lastname.GetHashCode() : 0);
        hashCode = (hashCode * 397) ^ Score;
        hashCode = (hashCode * 397) ^ Birth.GetHashCode();
        return hashCode;
      }
    }
  }
}