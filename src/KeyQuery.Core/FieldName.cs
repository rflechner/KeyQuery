using System.Threading;

namespace KeyQuery.Core
{
    public class FieldName
    {
        public string Value { get; }

        public FieldName(string value)
        {
            Value = value;
        }

        public static implicit operator string(FieldName name) => name.Value;

        public static implicit operator FieldName(string name) => new FieldName(name);

        public override string ToString() => Value;
    }
}
