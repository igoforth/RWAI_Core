using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Caching.Memory;

public class LazyCompressedDictionary
{
    private readonly MemoryCache _cache;
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly ConcurrentDictionary<int, long> _index = new ConcurrentDictionary<int, long>();

    public LazyCompressedDictionary(string filePath, int cacheSize = 1024)
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = cacheSize });
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        _filePath = filePath;
        LoadIndex();
    }

    public async Task AddOrUpdateAsync(int id, string title, string description)
    {
        var value = (Title: title, Description: description);
        byte[] compressedValue = CompressValue(value);
        await SaveEntryAsync(id, compressedValue);
        CacheValue(id, compressedValue);
    }

    public (string Title, string Description)? Get(int id)
    {
        if (_cache.TryGetValue(id, out byte[] compressedValue))
        {
            return DecompressValue(compressedValue);
        }

        var loadTask = LoadEntryAsync(id);
        loadTask.Wait();
        var loadedValue = loadTask.Result;

        if (loadedValue != null)
        {
            CacheValue(id, loadedValue);
            return DecompressValue(loadedValue);
        }

        return null;
    }

    private void CacheValue(int id, byte[] value)
    {
        _cache.Set(
            id,
            value,
            new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromMinutes(10) }
        );
    }

    private void LoadIndex()
    {
        if (!File.Exists(_filePath))
            return;

        using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fileStream);

        while (fileStream.Position < fileStream.Length)
        {
            var id = reader.ReadInt32();
            var position = reader.BaseStream.Position;
            var length = reader.ReadInt32();
            reader.BaseStream.Seek(length, SeekOrigin.Current);

            _index[id] = position;
        }
    }

    private async Task SaveEntryAsync(int id, byte[] value)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var fileStream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None,
                4096,
                true
            );
            using var writer = new BinaryWriter(fileStream);

            _index[id] = fileStream.Position;
            writer.Write(id);
            writer.Write(value.Length);
            writer.Write(value);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<byte[]> LoadEntryAsync(int id)
    {
        if (!_index.TryGetValue(id, out var position))
        {
            return null;
        }

        await _semaphore.WaitAsync();
        try
        {
            using var fileStream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
            );
            using var reader = new BinaryReader(fileStream);

            fileStream.Seek(position, SeekOrigin.Begin);
            var idInFile = reader.ReadInt32();
            var length = reader.ReadInt32();
            var value = reader.ReadBytes(length);

            return value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private byte[] CompressValue((string Title, string Description) value)
    {
        var combinedString = $"{value.Title}|{value.Description}";
        var bytes = Encoding.UTF8.GetBytes(combinedString);
        return LZ4Pickler.Pickle(bytes);
    }

    private (string Title, string Description) DecompressValue(byte[] compressed)
    {
        var bytes = LZ4Pickler.Unpickle(compressed);
        var combinedString = Encoding.UTF8.GetString(bytes);
        var parts = combinedString.Split('|');
        return (parts[0], parts[1]);
    }
}

// Example usage
// public class Program
// {
//     public static async Task Main(string[] args)
//     {
//         var dict = new LazyCompressedDictionary("dictionary.dat", cacheSize: 100);
//         await dict.AddOrUpdateAsync(1, "Title1", "Description1");
//         await dict.AddOrUpdateAsync(2, "Title2", "Description2");

//         var entry1 = dict.Get(1);
//         var entry2 = dict.Get(2);

//         Console.WriteLine($"ID 1: {entry1?.Title} - {entry1?.Description}");
//         Console.WriteLine($"ID 2: {entry2?.Title} - {entry2?.Description}");
//     }
// }
