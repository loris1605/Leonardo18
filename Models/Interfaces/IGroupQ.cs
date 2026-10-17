namespace Models.Interfaces
{
    public interface IGroupQ<T> : IDisposable where T : class
    {
        Task<List<T>> Load(int index = 0, CancellationToken ctk = default);
        Task<List<T>> LoadByModel(object model, CancellationToken ctk = default);
       

    }
}
