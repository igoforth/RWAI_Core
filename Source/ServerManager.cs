// asynchronous
// manages the python process (which manages the AI Server)
// sends gRPC to ai server to get generation
// updates the server status
//
namespace AICore;

using System.Diagnostics;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ServerStatus
{
    Online,
    Busy,
    Error,
    Offline
}

public class ServerManager : MonoBehaviour
{
    public TextMeshProUGUI logTextUI;
    public ScrollRect scrollRect;
    private UISink uiSink;

    public static bool serverRunning = false;
    public static ServerStatus serverStatusEnum = ServerStatus.Offline;
    public static string serverStatus = "AI Server " + ServerStatus.Offline;
    private Process serverProcess;

    // Update server status
    public static void UpdateServerStatus(ServerStatus status)
    {
        serverStatusEnum = status;
        serverStatus = "AI Server " + status.ToString();
        LogTool.Message(serverStatus, "UISink");
    }

    public void Start()
    {
        uiSink = UISink.Instance;
        uiSink.Initialize(logTextUI, scrollRect);

        // Start the process and capture its output
        StartProcessAndCaptureOutput("your_external_process.exe", "");
    }

    void StartProcessAndCaptureOutput(string fileName, string arguments)
    {
        try
        {
            UpdateServerStatus(ServerStatus.Busy);

            serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            serverProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    LogTool.Message(args.Data, "UISink");
                }
            };

            serverProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    LogTool.Error(args.Data, "UISink");
                }
            };

            serverProcess.Start();
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error starting process: {ex}");
            UpdateServerStatus(ServerStatus.Offline);
            return;
        }

        // Begin reading the output asynchronously
        serverProcess.BeginOutputReadLine();
        serverProcess.BeginErrorReadLine();

        UpdateServerStatus(ServerStatus.Online);
        serverRunning = true;

        // Run the server process
        Tools.SafeAsync(async () =>
        {
            while (serverRunning && AICoreMod.Running)
                await Task.Delay(200);

            ProcessHelper.SendSigINT(serverProcess);
            serverProcess.WaitForExit();
            serverProcess.Close();
            UpdateServerStatus(ServerStatus.Offline);
        });
    }
}
