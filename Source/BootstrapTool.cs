namespace AICore;

using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
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
    public static bool isConfigured;
    public static bool hasServerUpdate;
    private const string releaseString =
        "https://api.github.com/repos/igoforth/RWAILib/releases/latest";
    private const string bootstrapString =
        "https://github.com/igoforth/RWAILib/releases/latest/download/Bootstrap.py";
    private const string pythonString = "https://cosmo.zip/pub/cosmos/bin/python";
    private static readonly PlatformID platform = Environment.OSVersion.Platform;
    private static string modConfigPath;
    private static string pythonPath;
    private static string llamaPath;
    private static TaskCompletionSource<bool> eventHandled;
    private Process bootstrapProcess;

    private BootstrapTool()
    {
        modConfigPath = Path.Combine(GenFilePaths.ConfigFolderPath, "RWAI");
        pythonPath = Path.Combine(
            modConfigPath,
            "bin",
            platform == PlatformID.Win32NT ? "python.com" : "python"
        );
        llamaPath = Path.Combine(
            modConfigPath,
            "bin",
            platform == PlatformID.Win32NT ? "llamafile.com" : "llamafile"
        );
        isConfigured = checkConfigured();
        hasServerUpdate = checkServerUpdate();
    }

    private static bool checkConfigured()
    {
        var file_path_list = new[]
        {
            llamaPath,
            pythonPath,
            Path.Combine(modConfigPath, "models", "Phi-3-mini-128k-instruct.Q4_K_M.gguf"),
            Path.Combine(modConfigPath, "AIServer.zip"),
            Path.Combine(modConfigPath, ".version")
        };

        foreach (var file_path in file_path_list)
            if (!File.Exists(file_path))
                return false;
        return true;
    }

    // compare github api against pinned "./.version"
    private static bool checkServerUpdate()
    {
        // check for ".version" file
        var versionPath = Path.Combine(modConfigPath, ".version");
        if (!File.Exists(versionPath))
            return true;

        // check for internet
        internetAccess = checkInternet();
        if (!internetAccess)
            return false;

        // compare ".version" file with latest
        using var reader = File.OpenText(versionPath);
        var releaseUrl = new Uri(releaseString);
        var newRelease = "";
        var oldRelease = "";

        // compare online version
        try
        {
            Tools.SafeAsync(async () =>
            {
                Task<string> newReleaseT = Download(ContentType.String, releaseUrl);
                Task<string> oldReleaseT = reader.ReadToEndAsync();
                await Task.WhenAll(newReleaseT, oldReleaseT);
                newRelease = newReleaseT.Result;
                oldRelease = oldReleaseT.Result;
            });
        }
        catch (Exception ex)
        {
            LogTool.Error(ex.ToString());
        }
        finally
        {
            reader.Close();
        }

        JObject json = JObject.Parse(newRelease);
        string tag_name = (string)json["tag_name"];
        return tag_name != oldRelease;
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
        var bootstrapUrl = new Uri(bootstrapString);
        var pythonUrl = new Uri(pythonString);
        var binPath = Directory.GetParent(pythonPath).FullName;
        bool pythonDone = false;
        string scriptContent = "";

        LogTool.Message("RWAI has begun bootstrapping.");

        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath))
            Directory.CreateDirectory(binPath);

        // bootstrap python
        try
        {
            Tools.SafeAsync(async () =>
            {
                Task<string> pythonDownload = Download(ContentType.File, pythonUrl, pythonPath);
                Task<string> scriptDownload = Download(ContentType.String, bootstrapUrl);
                await Task.WhenAll(pythonDownload, scriptDownload);
                scriptContent = scriptDownload.Result;
                pythonDone = true;
            });
        }
        catch (Exception)
        {
            LogTool.Error("HTTP Error when downloading script or Python binary");
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
            // chmod python
            Tools.SafeAsync(async () =>
            {
                using var chmodProcess = new Process { StartInfo = chmodPSI };
                while (!pythonDone && AICoreMod.Running)
                    await Task.Delay(200);
                chmodProcess.Start();
                chmodProcess.WaitForExit();

                if (chmodProcess.ExitCode != 0)
                {
                    LogTool.Error("Failed to set executable permission on Python binary.");
                    return;
                }
            });
        }

        // Run the bootstrap process
        Tools.SafeAsync(async () =>
        {
            while (scriptContent == "" && AICoreMod.Running)
                await Task.Delay(200);
            await bt.Bootstrap(pythonPath, scriptContent);
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
            await Task.Delay(200);

        if (downloadHandler is DownloadHandlerFile)
            return default;

        return await Main.Perform(() =>
        {
            var result = downloadHandler.text;
            return result;
        });
    }

    public async Task Bootstrap(string pythonBin, string scriptContent)
    {
        eventHandled = new TaskCompletionSource<bool>();
        var pythonPSI = new ProcessStartInfo
        {
            FileName = pythonBin,
            WorkingDirectory = modConfigPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            bootstrapProcess = new Process { StartInfo = pythonPSI };
            bootstrapProcess.EnableRaisingEvents = true;
            bootstrapProcess.Exited += new EventHandler(ProcessExited);
            bootstrapProcess.Start();

            // Write the multiline script content to the process
            using var sw = bootstrapProcess.StandardInput;
            sw.WriteLine(scriptContent);
            sw.Close();

            // Read the output stream first and then wait.
            bootstrapProcess.BeginOutputReadLine();
            bootstrapProcess.BeginErrorReadLine();
            // #if DEBUG
            bootstrapProcess.OutputDataReceived += (sender, args) => LogTool.Message(args.Data);
            bootstrapProcess.ErrorDataReceived += (sender, args) => LogTool.Error(args.Data);
            // #endif
        }
        catch (Exception ex)
        {
            LogTool.Error($"Error starting process: {ex}");
            return;
        }

        await Task.WhenAny(eventHandled.Task);
    }

    // Handle Exited event and display process information.
    private void ProcessExited(object sender, EventArgs e)
    {
        LogTool.Message(
            $"Exit time    : {bootstrapProcess.ExitTime}\n"
                + $"Exit code    : {bootstrapProcess.ExitCode}\n"
                + $"Elapsed time : {Math.Round((bootstrapProcess.ExitTime - bootstrapProcess.StartTime).TotalMilliseconds)}"
        );
        eventHandled.TrySetResult(true);
    }
}
