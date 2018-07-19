using System.Runtime.Serialization;
using System.Threading;

namespace KeyQuery.Core
{
    [DataContract]
    public class FieldName
    {

        [DataMember]
        public string Value { get; private set; }

        public FieldName(string value)
        {
            Value = value;
        }

        public static implicit operator string(FieldName name) => name.Value;

        public static implicit operator FieldName(string name) => new FieldName(name);

        public override string ToString() => Value;

        protected bool Equals(FieldName other)
        {
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FieldName) obj);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }
    }
}
