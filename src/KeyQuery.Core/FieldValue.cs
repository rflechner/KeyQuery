using System;
using System.Runtime.Serialization;

namespace KeyQuery.Core
{
    [DataContract]
    public class FieldValue : IComparable<FieldValue>, IEquatable<FieldValue>
    {
        [DataMember]
        public string Value { get; private set; }

        public FieldValue(string value)
        {
            Value = value;
        }

        public static implicit operator string(FieldValue name) => name.Value;

        public static implicit operator FieldValue(string name) => new FieldValue(name);

        public override string ToString() => Value;

        public int CompareTo(FieldValue other)
        {
            return string.CompareOrdinal(Value, other.Value);
        }

        public bool Equals(FieldValue other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FieldValue) obj);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }
    }
}