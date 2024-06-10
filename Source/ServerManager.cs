// asynchronous
// manages the python process (which manages the AI Server)
// sends gRPC to ai server to get generation
// updates the server status
//
using System.Diagnostics;
using System.Text;
using Verse;

namespace AICore;

public class ServerManager : IDisposable
{
    public static CancellationTokenSource onQuit = new();
    public static bool Running => !onQuit.IsCancellationRequested;
    public static ServerStatus currentServerStatusEnum = ServerStatus.Offline;
    public static string currentServerStatus = "AI Server " + ServerStatus.Offline;
    private static readonly PlatformID platform = Environment.OSVersion.Platform;
    private static readonly string shellBin =
        platform == PlatformID.Win32NT ? "powershell.exe" : "sh";
#if DEBUG
    private const string serverArgs = "bin/python AIServer.pyz --loglevel DEBUG";
#else
    private const string serverArgs = "bin/python AIServer.pyz --loglevel INFO";
#endif
    private static string? modPath;
    private static ServerManager? instance;
    private Process? serverProcess;
    private static readonly object lockObject = new();

    // private FileSink logSink;

    public enum ServerStatus
    {
        Online,
        Busy,
        Error,
        Offline
    }

    private ServerManager()
    {
        modPath = Path.Combine(
            Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
            "RWAI"
        );
    }

    // singleton pattern
    public static ServerManager Instance
    {
        get
        {
            instance ??= new ServerManager();
            return instance;
        }
    }

    public void Start()
    {
        lock (lockObject)
        {
            if (Running && currentServerStatusEnum == ServerStatus.Online)
            {
                return; // Avoid starting if already running
            }

            // logSink = FileSink.Instance;
            StartProcess(shellBin, serverArgs);
        }
    }

    public void Stop()
    {
        lock (lockObject)
        {
            if (!Running || currentServerStatusEnum == ServerStatus.Offline)
            {
                return; // Avoid stopping if not running
            }

            onQuit.Cancel();
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.WaitForExit();
                serverProcess.Close();
            }
            // logSink.Dispose();
        }
    }

    // Update server status
    public static void UpdateServerStatus(ServerStatus status)
    {
        currentServerStatusEnum = status;
        currentServerStatus = "AI Server " + status.ToString();
        LogTool.Message(currentServerStatus, "ServerSink");
    }

    private void StartProcess(string shellBin, string shellArgs)
    {
        try
        {
            UpdateServerStatus(ServerStatus.Busy);

            serverProcess = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    FileName = shellBin,
                    Arguments = shellArgs,
                    WorkingDirectory = modPath,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            serverProcess.Exited += new EventHandler(ProcessExited);
            serverProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // LogTool.Error(args.Data, "ServerSink");
                    LogTool.Message(args.Data); // currently server stdout goes to stderr... idk why
                }
            };

#if DEBUG
            serverProcess.StartInfo.RedirectStandardOutput = true;
            serverProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            serverProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // LogTool.Debug(args.Data, "ServerSink");
                    LogTool.Debug(args.Data);
                }
            };
#endif
        }
        catch (InvalidOperationException ex)
        {
            LogTool.Error($"Invalid operation error starting process: {ex}");
            UpdateServerStatus(ServerStatus.Error);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            LogTool.Error($"Win32 error starting process: {ex}");
            UpdateServerStatus(ServerStatus.Error);
        }
        catch (PlatformNotSupportedException ex)
        {
            LogTool.Error($"Platform not supported error starting process: {ex}");
            UpdateServerStatus(ServerStatus.Error);
        }

        // Run the server process
        Tools.SafeAsync(async () =>
        {
            if (serverProcess == null) throw new InvalidOperationException(
                    "Server is trying to start without being initialized!"
                );

            serverProcess.Start();

#if DEBUG
            // Read the output stream first and then wait.
            serverProcess.BeginOutputReadLine();
#endif
            serverProcess.BeginErrorReadLine();

            UpdateServerStatus(ServerStatus.Online);
            LogTool.Message("AI Server started! OMG it actually started, whattttttt");

            while (Running && AICoreMod.Running) await Tools.SafeWait(200).ConfigureAwait(false);

            ProcessInterruptHelper.SendSigINT(serverProcess);
            serverProcess.WaitForExit();
            serverProcess.Close();
        });
    }

    // Handle Exited event and display process information.
    private void ProcessExited(object sender, EventArgs e)
    {
        if (serverProcess == null) return;

#if DEBUG
        LogTool.Debug($"Exit time    : {serverProcess.ExitTime}");
        LogTool.Debug($"Exit code    : {serverProcess.ExitCode}");
        LogTool.Debug(
            $"Elapsed time : {Math.Round((serverProcess.ExitTime - serverProcess.StartTime).TotalMilliseconds)}"
        );
#endif
        if (serverProcess.ExitCode != 0)
        {
            LogTool.Error("AI Server exited with non-zero code!");
        }

        LogTool.Message("AI Server shutdown.");
        UpdateServerStatus(ServerStatus.Offline);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            onQuit?.Dispose();
            serverProcess?.Dispose();
        }
    }
}
