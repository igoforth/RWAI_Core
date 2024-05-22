// asynchronous
// manages the python process (which manages the AI Server)
// sends gRPC to ai server to get generation
// updates the server status
//
namespace AICore;

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

public class ServerManager : MonoBehaviour
{
    public static CancellationTokenSource onQuit = new();
    public static bool Running => onQuit.IsCancellationRequested == false;
    public static ServerStatus serverStatusEnum = ServerStatus.Offline;
    public static string serverStatus = "AI Server " + ServerStatus.Offline;
    private static readonly PlatformID platform = Environment.OSVersion.Platform;
    private static readonly string shellBin =
        platform == PlatformID.Win32NT ? "powershell.exe" : "sh";
    private static string serverArgs = "bin/python AIServer.pyz";
    private static string modPath;
    private static ServerManager instance;
    private Process serverProcess;
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
            if (instance == null)
            {
                instance = new ServerManager();
            }
            return instance;
        }
    }

    public void Start()
    {
        lock (lockObject)
        {
            if (Running && serverStatusEnum == ServerStatus.Online)
                return; // Avoid starting if already running

            // logSink = FileSink.Instance;
            StartProcess(shellBin, serverArgs);
        }
    }

    public void Stop()
    {
        lock (lockObject)
        {
            if (!Running || serverStatusEnum == ServerStatus.Offline)
                return; // Avoid stopping if not running

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
        serverStatusEnum = status;
        serverStatus = "AI Server " + status.ToString();
        LogTool.Message(serverStatus, "ServerSink");
    }

    void StartProcess(string shellBin, string shellArgs)
    {
        try
        {
            UpdateServerStatus(ServerStatus.Busy);

            var serverPSI = new ProcessStartInfo
            {
                FileName = shellBin,
                Arguments = shellArgs,
                WorkingDirectory = modPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            serverProcess = new Process { StartInfo = serverPSI };
            serverProcess.EnableRaisingEvents = true;
            serverProcess.Exited += new EventHandler(ProcessExited);

            serverProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // LogTool.Message(args.Data, "ServerSink");
                    LogTool.Message(args.Data);
                }
            };

            serverProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // LogTool.Error(args.Data, "ServerSink");
                    LogTool.Message(args.Data);
                }
            };

            UpdateServerStatus(ServerStatus.Online);
            LogTool.Message("AI Server started! OMG it actually started, whattttttt");
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error starting process: {ex}");
            UpdateServerStatus(ServerStatus.Error);
            return;
        }

        // Run the server process
        Tools.SafeAsync(async () =>
        {
            serverProcess.Start();

            // Read the output stream first and then wait.
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();

            while (Running && AICoreMod.Running)
                await Tools.SafeWait(200);

            ProcessInterruptHelper.SendSigINT(serverProcess);
            serverProcess.WaitForExit();
            serverProcess.Close();
        });
    }

    // Handle Exited event and display process information.
    private void ProcessExited(object sender, EventArgs e)
    {
#if DEBUG
        LogTool.Debug($"Exit time    : {serverProcess.ExitTime}");
        LogTool.Debug($"Exit code    : {serverProcess.ExitCode}");
        LogTool.Debug(
            $"Elapsed time : {Math.Round((serverProcess.ExitTime - serverProcess.StartTime).TotalMilliseconds)}"
        );
#endif
        if (serverProcess.ExitCode != 0)
            LogTool.Error("AI Server exited with non-zero code!");
        LogTool.Message("AI Server shutdown.");
        UpdateServerStatus(ServerStatus.Offline);
    }
}
