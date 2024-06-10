using System.Collections.Concurrent;
using System.IO.Compression;
using Newtonsoft.Json;

namespace AICore;

public class CachedGZipStorage<T> : IDisposable
    where T : struct
{
    private readonly ConcurrentDictionary<int, (byte[] Value, DateTime Expiry)> _cache;
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<int, long> _index = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
    private readonly int _cacheSize;
    private readonly object _cacheLock = new();

    public CachedGZipStorage(string filePath, int cacheSize = 1024)
    {
        _cache = new ConcurrentDictionary<int, (byte[] Value, DateTime Expiry)>();
        _cacheSize = cacheSize;
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            _ = Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        _filePath = filePath;
        LoadIndex();
    }

    public async Task AddOrUpdateAsync(int id, T value)
    {
        byte[] compressedValue = CachedGZipStorage<T>.CompressValue(value);
        await SaveEntryAsync(id, compressedValue).ConfigureAwait(false);
        CacheValue(id, compressedValue);
    }

    public T? Get(int id)
    {
        if (_cache.TryGetValue(id, out var cachedValue) && cachedValue.Expiry > DateTime.UtcNow)
            return CachedGZipStorage<T>.DecompressValue(cachedValue.Value);


        var loadTask = LoadEntryAsync(id);
        loadTask.Wait();
        var loadedValue = loadTask.Result;

        if (loadedValue != null)
        {
            CacheValue(id, loadedValue);
            return CachedGZipStorage<T>.DecompressValue(loadedValue);
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
                    _ = _cache.TryRemove(key, out _);
                }
                if (_cache.Count >= _cacheSize)
                {
                    var oldestKey = _cache.OrderBy(pair => pair.Value.Expiry).FirstOrDefault().Key;
                    _ = _cache.TryRemove(oldestKey, out _);
                }
            }

            _cache[id] = (value, DateTime.UtcNow.Add(_cacheExpiration));
        }
    }

    private void LoadIndex()
    {
        if (!File.Exists(_filePath)) return;

        using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fileStream);

        while (fileStream.Position < fileStream.Length)
        {
            var id = reader.ReadInt32();
            var length = reader.ReadInt32();
            var position = reader.BaseStream.Position;
            _ = reader.BaseStream.Seek(length, SeekOrigin.Current);

            _index[id] = position;
        }
    }

    private async Task SaveEntryAsync(int id, byte[] value)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
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
            _ = _semaphore.Release();
        }
    }

    private async Task<byte[]?> LoadEntryAsync(int id)
    {
        if (!_index.TryGetValue(id, out var position)) return null;

        await _semaphore.WaitAsync().ConfigureAwait(false);
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

            _ = fileStream.Seek(position, SeekOrigin.Begin);
            var idInFile = reader.ReadInt32();
            var length = reader.ReadInt32();
            var value = reader.ReadBytes(length);

            return value;
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    private static byte[] CompressValue(T value)
    {
#if DEBUG
        LogTool.Debug($"Storing Value: {value}");
#endif
        try
        {
            var jsonSerializer = new JsonSerializer();
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    using (var streamWriter = new StreamWriter(compressionStream))
                    {
                        using (var jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            jsonSerializer.Serialize(jsonWriter, value);
                        }
                    }
                }
                return outputStream.ToArray();
            }
        }
        catch (JsonSerializationException ex)
        {
            LogTool.Error($"Serialization error: {ex.Message}");
            throw;
        }
    }

    private static T DecompressValue(byte[] compressed)
    {
        try
        {
            var jsonSerializer = new JsonSerializer();
            using (var inputStream = new MemoryStream(compressed))
            {
                using (var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    using (var streamReader = new StreamReader(decompressionStream))
                    {
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var deserializedValue = jsonSerializer.Deserialize<T>(jsonReader);
#if DEBUG
                            LogTool.Debug($"Retrieving value: {deserializedValue}");
#endif
                            return deserializedValue is T value
                                && !EqualityComparer<T>.Default.Equals(value, default)
                                ? value : throw new InvalidOperationException("Deserialized value is null");
                        }
                    }
                }
            }
        }
        catch (JsonSerializationException ex)
        {
            LogTool.Error($"Deserialization error: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) _semaphore?.Dispose();
    }
}
