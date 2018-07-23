using System;
using System.Runtime.Serialization;
using KeyQuery;
using KeyQuery.Core;

namespace WebSample1
{
    [DataContract]
    public class Customer : IDto<Guid>, IEquatable<Customer>
    {
        [DataMember] public Guid Id { get; private set; }
        [DataMember] public string FirstName { get; private set; }
        [DataMember] public string Lastname { get; private set; }
        [DataMember] public int Score { get; private set; }
        [DataMember] public DateTime Birth { get; private set; }

        private Customer()
        {
            
        }
        
        public Customer(Guid id, string firstName, string lastname, int score, DateTime birth)
        {
            Id = id;
            FirstName = firstName;
            Lastname = lastname;
            Score = score;
            Birth = birth;
        }

        public bool Equals(Customer other)
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
            return Equals((Customer) obj);
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