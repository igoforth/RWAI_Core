namespace AICore;

using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

public enum ContentType
{
    File,
    String
}

// once configured, layout should look like this:
// root:
//   Darwin  - ~/Library/Application Support/RimWorld/Config/RWAI/
//   Windows - %USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\RWAI\
//   Linux   - ~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/RWAI/
// files:
// ./bin/llamafile (win llamafile.com)
// ./bin/python (win python.com)
// ./models/Phi-3-mini-128k-instruct.Q4_K_M.gguf
// ./AIServer.zip
// ./.version
public sealed class BootstrapTool
{
    public static bool internetAccess = false;
    public static bool isConfigured = true;
    private const string releaseString =
        "https://api.github.com/repos/igoforth/RWAILib/releases/latest";
    private const string bootstrapString =
        "https://github.com/igoforth/RWAILib/releases/latest/download/Bootstrap.py";
    private const string pythonString = "https://cosmo.zip/pub/cosmos/bin/python";
    private static readonly PlatformID platform = Environment.OSVersion.Platform;
    private static readonly string shellBin =
        platform == PlatformID.Win32NT ? "powershell.exe" : "sh";
    private static string oldRelease = "_";
    private static string newRelease = "_";
    private static string modPath;
    private static string pythonPath;
    private static string llamaPath;
    private static TaskCompletionSource<bool> shouldUpdate;
    private static TaskCompletionSource<bool> bootstrapDone;
    private Process bootstrapProcess;

    private BootstrapTool()
    {
        modPath = Path.Combine(
            Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
            "RWAI"
        );
        pythonPath = Path.Combine(
            modPath,
            "bin",
            platform == PlatformID.Win32NT ? "python.com" : "python"
        );
        llamaPath = Path.Combine(
            modPath,
            "bin",
            platform == PlatformID.Win32NT ? "llamafile.com" : "llamafile"
        );
        isConfigured = checkConfigured();
    }

    private static bool checkConfigured()
    {
        var file_path_list = new[]
        {
            llamaPath,
            pythonPath,
            Path.Combine(modPath, "models", "Phi-3-mini-128k-instruct.Q4_K_M.gguf"),
            Path.Combine(modPath, "AIServer.pyz"),
            Path.Combine(modPath, ".version")
        };

        foreach (var file_path in file_path_list)
            if (!File.Exists(file_path))
                return false;
        return true;
    }

    // compare github api against pinned "./.version"
    private static async Task checkServerUpdate()
    {
        bool triggerUpdate = false;
        try
        {
            // check for ".version" file
            var versionPath = Path.Combine(modPath, ".version");
            if (!File.Exists(versionPath))
            {
                triggerUpdate = true;
                goto setUpdate;
            }

            // check for internet
            internetAccess = checkInternet();
            if (!internetAccess)
            {
                triggerUpdate = false;
                goto setUpdate;
            }

            // compare ".version" file with latest
            var releaseUrl = new Uri(releaseString);
            using (var reader = File.OpenText(versionPath))
            {
                Task<string> newReleaseT = Download(ContentType.String, releaseUrl);
                Task<string> oldReleaseT = reader.ReadToEndAsync();

                while (AICoreMod.Running)
                    await Task.WhenAll(newReleaseT, oldReleaseT);

                newRelease = newReleaseT.Result;
                LogTool.Message($"New version: {newRelease}");
                oldRelease = oldReleaseT.Result;
                LogTool.Message($"Old version: {newRelease}");
                var json = JObject.Parse(newRelease);
                string tag_name = (string)json["tag_name"];
                if (tag_name != oldRelease)
                {
                    triggerUpdate = true;
                }
            }
        }
        catch (Exception ex)
        {
            triggerUpdate = false;
            LogTool.Error(ex.ToString());
        }
        setUpdate:
        if (triggerUpdate)
            shouldUpdate.TrySetResult(true);
        else
            shouldUpdate.TrySetResult(false);
    }

    // check for internet access
    public static bool checkInternet()
    {
        try
        {
            using var pingSender = new Ping();
            var pingReply = pingSender.Send("dns.google");
            if (pingReply.Status == IPStatus.Success)
                return true;
        }
        catch (PingException)
        {
            return false;
        }
        return false;
    }

    public static void Run()
    {
        BootstrapTool bt = new BootstrapTool();
        shouldUpdate = new TaskCompletionSource<bool>();
        bootstrapDone = new TaskCompletionSource<bool>();

        // Check for updates
        Tools.SafeAsync(async () =>
        {
            var csu = checkServerUpdate();
            while (csu.Status == TaskStatus.Running)
            {
                await Tools.SafeWait(200);
                if (!AICoreMod.Running)
                    shouldUpdate.SetCanceled();
            }
        });

        // Run bootstrapper
        Tools.SafeAsync(async () =>
        {
            while (shouldUpdate.Task.Status == TaskStatus.Running)
                await Tools.SafeWait(200);
            if (shouldUpdate.Task.IsCanceled || shouldUpdate.Task.IsFaulted)
                return;
            if (shouldUpdate.Task.Result == false)
            {
                LogTool.Message($"You are running version {oldRelease}");
                return;
            }
            var ba = bt.BootstrapAsync();
            while (ba.Status == TaskStatus.Running)
            {
                await Tools.SafeWait(200);
                if (!AICoreMod.Running)
                    bootstrapDone.SetCanceled();
            }
        });

        // Do other things
        Tools.SafeAsync(async () =>
        {
            while (bootstrapDone.Task.Status == TaskStatus.Running)
                await Tools.SafeWait(200);
            if (bootstrapDone.Task.IsCanceled || bootstrapDone.Task.IsFaulted)
                return;
        });
    }

    private static async Task<string> Download(
        ContentType content,
        Uri fileUrl,
        string destination = null
    )
    {
        string filePath = destination;
        using var request = UnityWebRequest.Get(fileUrl);
        request.method = "GET";

        switch (content)
        {
            case ContentType.File:
#if DEBUG
                Debug.Assert(destination != null, "Destination cannot be null");
#endif

                if (File.Exists(destination))
                    File.Delete(destination);
                else if (Directory.Exists(destination))
                    filePath = Path.Combine(destination, Path.GetFileName(fileUrl.LocalPath));

                break;
            case ContentType.String:
            default:
                break;
        }

        using DownloadHandler downloadHandler =
            destination != null ? new DownloadHandlerFile(filePath) : new DownloadHandlerBuffer();

        if (downloadHandler is DownloadHandlerFile fileHandler)
            fileHandler.removeFileOnAbort = true;

        request.downloadHandler = downloadHandler;
        var asyncOperation = request.SendWebRequest();
        while (!asyncOperation.isDone && AICoreMod.Running)
            await Tools.SafeWait(200);

        if (request.error != null)
            throw new Exception(request.error);

        if (downloadHandler is DownloadHandlerFile)
            return default;

        return await Main.Perform(() =>
        {
            var result = downloadHandler.text;
            return result;
        });
    }

    public async Task BootstrapAsync()
    {
        var bootstrapUrl = new Uri(bootstrapString);
        var pythonUrl = new Uri(pythonString);
        var binPath = Directory.GetParent(pythonPath).FullName;
        string scriptContent = "";

        LogTool.Message("RWAI has begun bootstrapping!");
        ServerManager.UpdateServerStatus(ServerStatus.Busy);

        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath))
            Directory.CreateDirectory(binPath);

        try
        {
            Task<string> pythonDownload = Download(ContentType.File, pythonUrl, pythonPath);
            Task<string> scriptDownload = Download(ContentType.String, bootstrapUrl);
            await Task.WhenAll(pythonDownload, scriptDownload);
            scriptContent = scriptDownload.Result;
        }
        catch (Exception ex)
        {
            LogTool.Error("HTTP Error when downloading script or Python binary");
            LogTool.Error($"{ex.Message}");
            return;
        }

        // If OS is not Windows, make python executable
        if (platform != PlatformID.Win32NT)
        {
            var chmodPSI = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x {pythonPath}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var chmodProcess = new Process { StartInfo = chmodPSI };
            chmodProcess.Start();
            chmodProcess.WaitForExit();

            if (chmodProcess.ExitCode != 0)
            {
                LogTool.Error("Failed to set executable permission on Python binary.");
                return;
            }
        }

        var pythonPSI = new ProcessStartInfo
        {
            FileName = shellBin,
            Arguments = "bin/python -",
            WorkingDirectory = modPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            bootstrapProcess = new Process { StartInfo = pythonPSI };
            bootstrapProcess.EnableRaisingEvents = true;
            bootstrapProcess.Exited += new EventHandler(ProcessExited);

            bootstrapProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // LogTool.Message(args.Data, "UISink");
                    LogTool.Message(args.Data);
                }
            };

            bootstrapProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    // LogTool.Error(args.Data, "UISink");
                    LogTool.Error(args.Data);
                }
            };

            bootstrapProcess.Start();

            // Write the multiline script content to the process
            using (var sw = bootstrapProcess.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(scriptContent);
                }
                sw.Close();
            }

            // Read the output stream first and then wait.
            bootstrapProcess.BeginOutputReadLine();
            bootstrapProcess.BeginErrorReadLine();

            bootstrapProcess.WaitForExit();
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error starting process: {ex}");
            return;
        }

        await Task.WhenAny(bootstrapDone.Task);
    }

    // Handle Exited event and display process information.
    private void ProcessExited(object sender, EventArgs e)
    {
#if DEBUG
        LogTool.Debug($"Exit time    : {bootstrapProcess.ExitTime}");
        LogTool.Debug($"Exit code    : {bootstrapProcess.ExitCode}");
        LogTool.Debug(
            $"Elapsed time : {Math.Round((bootstrapProcess.ExitTime - bootstrapProcess.StartTime).TotalMilliseconds)}"
        );
#endif
        if (bootstrapProcess.ExitCode != 0)
            ServerManager.UpdateServerStatus(ServerStatus.Offline);
        bootstrapDone.TrySetResult(true);
    }
}
