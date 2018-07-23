namespace KeyQuery.Core
{
    public interface IDto<TId>
    {
        TId Id { get; }
    }
}