namespace AICore;

using System.Collections.Concurrent;
using System.Collections.Generic;

public static class LogTool
{
    class Msg
    {
        internal string txt;
        internal int level;
        internal string sinkName;
    }

    private static readonly ConcurrentQueue<Msg> log = new();
    private static readonly List<IUISink> sinks = new(); // List of log sinks

    public static void AddSink(IUISink sink)
    {
        sinks.Add(sink);
    }

    public static void RemoveSink(IUISink sink)
    {
        sinks.Remove(sink);
    }

    public static void Message(string txt, string sinkName = null)
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

    public static void Warning(string txt, string sinkName = null)
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

    public static void Error(string txt, string sinkName = null)
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
    public static void Debug(string txt, string sinkName = null)
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
        while (log.Count > 0 && AICoreMod.Running)
        {
            if (log.TryDequeue(out var msg) == false)
                continue;
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
                }
            }

            // Write to all registered sinks
            foreach (var sink in sinks)
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
public interface IUISink : IDisposable
{
    string Name { get; }
    void Write(string formattedLogMessage, int level);
}
