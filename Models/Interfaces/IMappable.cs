namespace Models.Interfaces
{
    public interface IMappable<TTable> where TTable : class, new()
    {
        TTable ToTable();
        void UpdateTable(TTable existing);
    }
}
