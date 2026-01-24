using System.Threading.Tasks;

namespace White_Desert.Services.Contracts;

public interface IFileService<T> where T : class, new()
{
    public T? Data { get;  }
    
    Task SaveAsync();
    Task SaveAsync(T data);
    T Load();
    Task<T> LoadAsync();
}