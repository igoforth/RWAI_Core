using System.Collections.Concurrent;

namespace AICore;

public static class LogTool
{
    private sealed class Msg
    {
        internal required string txt;
        internal int level;
        internal string? sinkName;
    }

    private static readonly ConcurrentQueue<Msg> log = new();
    private static readonly List<ISink> sinks = []; // List of log sinks

    public static void AddSink(ISink sink)
    {
        sinks.Add(sink);
    }

    public static void RemoveSink(ISink sink)
    {
        _ = sinks.Remove(sink);
    }

    public static void Message(string txt, string? sinkName = null)
    {
        log.Enqueue(
            new Msg()
            {
                txt = txt,
                level = 0,
                sinkName = sinkName
            }
        );
    }

    public static void Warning(string txt, string? sinkName = null)
    {
        log.Enqueue(
            new Msg()
            {
                txt = txt,
                level = 1,
                sinkName = sinkName
            }
        );
    }

    public static void Error(string txt, string? sinkName = null)
    {
        log.Enqueue(
            new Msg()
            {
                txt = txt,
                level = 2,
                sinkName = sinkName
            }
        );
    }

#if DEBUG
    public static void Debug(string txt, string? sinkName = null)
    {
        log.Enqueue(
            new Msg()
            {
                txt = txt,
                level = 0,
                sinkName = sinkName
            }
        );
    }
#endif

    internal static void Log()
    {
        while (!log.IsEmpty && AICoreMod.Running)
        {
            if (!log.TryDequeue(out Msg? msg))
            {
                continue;
            }

            string formattedMessage = FormatMessage(msg.level, msg.txt);

            // Write to Verse.Log if no specific sink is targeted
            if (msg.sinkName == null)
            {
                switch (msg.level)
                {
                    case 0:
                        Verse.Log.Message(formattedMessage);
                        break;
                    case 1:
                        Verse.Log.Warning(formattedMessage);
                        break;
                    case 2:
                        Verse.Log.Error(formattedMessage);
                        break;
                    default:
                        break;
                }
            }

            // Write to all registered sinks
            foreach (ISink sink in sinks)
            {
                if (msg.sinkName == null || sink.Name == msg.sinkName)
                {
                    sink.Write(formattedMessage, msg.level);
                }
            }
        }
    }

    private static string FormatMessage(int level, string txt)
    {
        string prefix = level switch
        {
            0 => "[RWAI Message] ",
            1 => "[RWAI Warning] ",
            2 => "[RWAI Error] ",
            _ => "[RWAI Unknown] "
        };
        return prefix + txt;
    }
}

// Interface for sinks to implement
public interface ISink : IDisposable
{
    string Name { get; }
    void Write(string formattedLogMessage, int level);
}
