using Newtonsoft.Json;
using Verse;

namespace AICore;

public class FileSink : ISink
{
    public string Name => "FileSink";
    private static FileSink? instance;
    private static string? modPath;
    private static string? serverLogPath;
    private static StreamWriter? streamWriter;

    private FileSink()
    {
        modPath = Path.Combine(
            Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
            "RWAI"
        );
        serverLogPath = Path.Combine(modPath, "Server.log");

        // Initialize the log file
        if (File.Exists(serverLogPath))
        {
            File.Delete(serverLogPath);
        }
        else
        {
            _ = Directory.CreateDirectory(modPath);
        }

        // Create the log file and keep the stream open
        streamWriter = new StreamWriter(serverLogPath, true) { AutoFlush = true };
    }

    // Singleton pattern
    public static FileSink Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new FileSink();
                LogTool.AddSink(instance);
            }
            return instance;
        }
    }

    private static void LogMessage(string message, int level)
    {
        if (streamWriter == null)
        {
            return;
        }

        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message
        };

        // Asynchronous file write
        // serious marshalling problems, figure out later
        Tools.SafeAsync(async () =>
        {
            string jsonLogEntry = JsonConvert.SerializeObject(logEntry);
            await streamWriter.WriteLineAsync(jsonLogEntry).ConfigureAwait(false);
        });
    }

    public void Write(string formattedLogMessage, int level)
    {
        LogMessage(formattedLogMessage, level);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            LogTool.RemoveSink(this); // Remove from LogTool's sinks
            streamWriter?.Dispose(); // Dispose the StreamWriter
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~FileSink()
    {
        Dispose(false);
    }
}
