using White_Desert.Services.Contracts;

namespace White_Desert.Services.Implementations;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class FileService<T> : IFileService<T> where T : class, new()
{
    private readonly string _folderPath;
    private readonly string _filePath;

    public T? Data { get; private set; }
    
    public FileService()
    {
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "White Desert");
        
        _filePath = Path.Combine(_folderPath, $"{typeof(T).Name}.json");
    }

    public async Task SaveAsync()
    {
        if(Data == null) return;
        
        await SaveAsync(Data);
    }

    public async Task SaveAsync(T data)
    {
        if (!Directory.Exists(_folderPath))
            Directory.CreateDirectory(_folderPath);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(data, options);
        
        await File.WriteAllTextAsync(_filePath, json);
    }
    
    public T Load()
    {
        if (!File.Exists(_filePath))
        {
            Data = new T();
            return Data;
        }
            

        var json = File.ReadAllText(_filePath);
        
        Data = JsonSerializer.Deserialize<T>(json) ?? new T();

        return Data;
    }

    public async Task<T> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            Data = new T();
            return Data;
        }
            

        var json = await File.ReadAllTextAsync(_filePath);
        
        Data = JsonSerializer.Deserialize<T>(json) ?? new T();

        return Data;
    }
}