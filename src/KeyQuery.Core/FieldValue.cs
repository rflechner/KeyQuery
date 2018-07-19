namespace KeyQuery.Core
{
    public class FieldValue
    {
        public string Value { get; }

        public FieldValue(string value)
        {
            Value = value;
        }

        public static implicit operator string(FieldValue name) => name.Value;

        public static implicit operator FieldValue(string name) => new FieldValue(name);

        public override string ToString() => Value;
    }
}