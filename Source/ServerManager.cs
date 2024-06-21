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
    public static string currentServerStatus = ("RWAI_" + ServerStatus.Offline.ToString()).Translate();
    private static readonly PlatformID platform = Environment.OSVersion.Platform;
    private static ServerManager? instance;
    private static readonly object lockObject = new();
    private static Process? serverProcess;

    public enum ServerStatus
    {
        Online,
        Busy,
        Error,
        Offline
    }

    private ServerManager() { }

    // singleton pattern
    public static ServerManager Instance
    {
        get
        {
            instance ??= new ServerManager();
            return instance;
        }
    }

    public static void UpdateRunningState(bool enabled)
    {
        // if BootstrapTool is not running, then start
        if (enabled && currentServerStatusEnum != ServerStatus.Busy) Start();
        else Stop();
    }

    public static void Start()
    {
        lock (lockObject)
        {
            // Avoid starting if already running
            if (Running && currentServerStatusEnum == ServerStatus.Online) return;

            // logSink = FileSink.Instance;

            // Reset the CancellationTokenSource when starting
            onQuit.Dispose();
            onQuit = new CancellationTokenSource();

            StartProcess(onQuit.Token);
        }
    }

    public static void Stop()
    {
        lock (lockObject)
        {
            // Avoid stopping if not running
            if (!Running || currentServerStatusEnum == ServerStatus.Offline) return;

            onQuit.Cancel();
            onQuit.Dispose();
        }
    }

    // Update server status
    public static void UpdateServerStatus(ServerStatus status)
    {
        currentServerStatusEnum = status;
        currentServerStatus = ("RWAI_" + status.ToString()).Translate();
        LogTool.Message(currentServerStatus, "ServerSink");
    }

    private static void StartProcess(CancellationToken token)
    {
        var shellBin =
            platform == PlatformID.Win32NT ? "powershell.exe" : "sh";
#if DEBUG
        var shellArgs = "bin/python AIServer.pyz --loglevel DEBUG";
#else
        var shellArgs = "bin/python AIServer.pyz --loglevel INFO";
#endif
        var modPath = Path.Combine(
            Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
            "RWAI"
        );

        _ = Task.Run(async () => await ManageServerAsync(shellBin, shellArgs, modPath, token).ConfigureAwait(false), token);
    }

    private static async Task ManageServerAsync(string shellBin, string shellArgs, string modPath, CancellationToken token)
    {
        // Run the server process
        try
        {
            // UpdateServerStatus(ServerStatus.Busy);
            using (serverProcess = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    FileName = shellBin,
                    Arguments = shellArgs,
                    WorkingDirectory = modPath,
                    RedirectStandardInput = false,
#if DEBUG
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
#endif
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                serverProcess.Start();

#if DEBUG
                // Read the output stream first and then wait.
                serverProcess.BeginOutputReadLine();
#endif
                serverProcess.BeginErrorReadLine();

                serverProcess.Exited += (sender, e) =>
                {
                    if (serverProcess == null) return;
#if DEBUG
                    var elapsedTime = Math.Round(
                        (serverProcess.ExitTime - serverProcess.StartTime).TotalMilliseconds
                    );
                    LogTool.Debug($"Exit time    : {serverProcess.ExitTime}");
                    LogTool.Debug($"Exit code    : {serverProcess.ExitCode}");
                    LogTool.Debug($"Elapsed time : {elapsedTime}");
#endif
                    if (serverProcess.ExitCode != 0) LogTool.Warning($"AI Server exited with non-zero code: {serverProcess.ExitCode}");
                    else LogTool.Message("AI Server shutdown.");
                    UpdateServerStatus(ServerStatus.Offline);
                };

                serverProcess.ErrorDataReceived += (sender, args) =>
                {
                    // currently server stdout goes to stderr... idk why
                    if (!string.IsNullOrEmpty(args.Data)) LogTool.Message(args.Data);
                };

#if DEBUG
                serverProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) LogTool.Debug(args.Data);
                };
#endif

                UpdateServerStatus(ServerStatus.Online);
                LogTool.Message("AI Server started! OMG it actually started, whattttttt");

                // Handling cancellation
                using (var registration = token.Register(() =>
                {
                    if (!serverProcess.HasExited) ProcessInterruptHelper.SendSigINT(serverProcess);
                }))
                {
                    await Task.Run(serverProcess.WaitForExit, token).ConfigureAwait(false);
                }
            }
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
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) Stop();
    }
}
