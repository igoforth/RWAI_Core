using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImpromptuNinjas.ZStd;
using Newtonsoft.Json;

namespace AICore;

public class CompressedDictionary<T>
    where T : struct
{
    private readonly ConcurrentDictionary<int, (byte[] Value, DateTime Expiry)> _cache;
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<int, long> _index = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
    private readonly int _cacheSize;
    private readonly object _cacheLock = new();

    public CompressedDictionary(string filePath, int cacheSize = 1024)
    {
        _cache = new ConcurrentDictionary<int, (byte[] Value, DateTime Expiry)>();
        _cacheSize = cacheSize;
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        _filePath = filePath;
        LoadIndex();
    }

    public async Task AddOrUpdateAsync(int id, T value)
    {
        byte[] compressedValue = CompressValue(value);
        await SaveEntryAsync(id, compressedValue);
        CacheValue(id, compressedValue);
    }

    public T? Get(int id)
    {
        if (_cache.TryGetValue(id, out var cachedValue) && cachedValue.Expiry > DateTime.UtcNow)
            return DecompressValue(cachedValue.Value);

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
        lock (_cacheLock)
        {
            if (_cache.Count >= _cacheSize)
            {
                var expiredKeys = _cache
                    .Where(pair => pair.Value.Expiry <= DateTime.UtcNow)
                    .Select(pair => pair.Key)
                    .ToList();
                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }
                if (_cache.Count >= _cacheSize)
                {
                    var oldestKey = _cache.OrderBy(pair => pair.Value.Expiry).FirstOrDefault().Key;
                    _cache.TryRemove(oldestKey, out _);
                }
            }

            _cache[id] = (value, DateTime.UtcNow.Add(_cacheExpiration));
        }
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
            var length = reader.ReadInt32();
            var position = reader.BaseStream.Position;
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

    private async Task<byte[]?> LoadEntryAsync(int id)
    {
        if (!_index.TryGetValue(id, out var position))
            return null;

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

    private byte[] CompressValue(T value)
    {
#if DEBUG
        LogTool.Debug($"Storing Value: {value}");
#endif
        using var outputStream = new MemoryStream();
        using var compressionStream = new ZStdCompressStream(outputStream);
        using var streamWriter = new StreamWriter(compressionStream);
        var serializer = new JsonSerializer();
        serializer.Serialize(streamWriter, value);
        streamWriter.Flush();
        return outputStream.ToArray();
    }

    private T DecompressValue(byte[] compressed)
    {
        using var inputStream = new MemoryStream(compressed);
        using var decompressionStream = new ZStdDecompressStream(inputStream);
        using var streamReader = new StreamReader(decompressionStream);
        var serializer = new JsonSerializer();
        return (T)serializer.Deserialize(streamReader, typeof(T));
    }
}
