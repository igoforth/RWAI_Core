namespace AICore;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private static string oldRelease = "_";
    private static string newRelease = "_";
    private static OSPlatform platform;
    private static Architecture arch;
    private static string shellBin;
    private static string modPath;
    private static string pythonPath;
    private static string llamaPath;
    private static TaskCompletionSource<bool> shouldUpdate;
    private static TaskCompletionSource<bool> bootstrapDone;
    private Process bootstrapProcess;

    private BootstrapTool()
    {
        getSystemInfo();
        shellBin = platform == OSPlatform.Windows ? "powershell.exe" : "sh";
        modPath = Path.Combine(
            Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
            "RWAI"
        );
        pythonPath = Path.Combine(
            modPath,
            "bin",
            platform == OSPlatform.Windows ? "python.com" : "python"
        );
        llamaPath = Path.Combine(
            modPath,
            "bin",
            platform == OSPlatform.Windows ? "llamafile.com" : "llamafile"
        );
        isConfigured = checkConfigured();
        setGrpcOverrideLocation();
    }

    private static bool checkConfigured()
    {
        var directory_path_list = new[]
        {
            Path.Combine(modPath, "bin"),
            Path.Combine(modPath, "models")
        };

        foreach (var file_path in directory_path_list)
            if (!Directory.Exists(file_path))
                return false;

        // TODO: do something smarter to detect models available
        var files = Directory.GetFiles(Path.Combine(modPath, "models"));
        if (files.Length == 0)
            return false;

        var file_path_list = new[]
        {
            llamaPath,
            pythonPath,
            Path.Combine(modPath, "AIServer.pyz"),
            Path.Combine(modPath, ".version")
        };

        foreach (var file_path in file_path_list)
            if (!File.Exists(file_path))
                return false;

        return true;
    }

    // compare github api against pinned "./.version"
    private async void checkServerUpdate()
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
            var userAgent = "igoforth/RWAILib";
            using (var reader = File.OpenText(versionPath))
            {
                Task<string> newReleaseT = Download(ContentType.String, releaseUrl, userAgent);
                Task<string> oldReleaseT = reader.ReadToEndAsync();
                await Task.WhenAll(newReleaseT, oldReleaseT);

                newRelease = newReleaseT.Result;
                oldRelease = oldReleaseT.Result;

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

    private static void getSystemInfo()
    {
        platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OSPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? OSPlatform.Linux
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? OSPlatform.OSX
                    : throw new PlatformNotSupportedException("Unsupported OS platform.");

        arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => Architecture.X64,
            Architecture.X86 => Architecture.X86,
            Architecture.Arm64 => Architecture.Arm64,
            _ => throw new PlatformNotSupportedException("Unsupported architecture.")
        };
    }

    private static void setGrpcOverrideLocation()
    {
        var basePath = Path.GetFullPath(
            Path.Combine(Assembly.GetCallingAssembly().Location, @"..\..\Libraries\")
        );
        var libraryMapping = new Dictionary<(OSPlatform, Architecture), string>
        {
            { (OSPlatform.Windows, Architecture.X64), "grpc_csharp_ext.x64.dll" },
            { (OSPlatform.Windows, Architecture.X86), "grpc_csharp_ext.x86.dll" },
            { (OSPlatform.Linux, Architecture.X64), "libgrpc_csharp_ext.x64.so" },
            { (OSPlatform.OSX, Architecture.Arm64), "libgrpc_csharp_ext.arm64.dylib" },
            { (OSPlatform.OSX, Architecture.X64), "libgrpc_csharp_ext.x64.dylib" }
        };

        if (libraryMapping.TryGetValue((platform, arch), out var libraryName))
            Environment.SetEnvironmentVariable(
                "GRPC_CSHARP_EXT_OVERRIDE_LOCATION",
                Path.Combine(basePath, libraryName)
            );
        else
            throw new NotSupportedException("Unsupported OS and Architecture combination.");
    }

    public static void Run()
    {
        BootstrapTool bt = new BootstrapTool();
        shouldUpdate = new TaskCompletionSource<bool>();
        var updateTask = shouldUpdate.Task;
        bootstrapDone = new TaskCompletionSource<bool>();
        var bootstrapTask = bootstrapDone.Task;

        // Check for updates
        Tools.SafeAsync(async () =>
        {
            // this is where checkConfigured is called
            // player will have option to not check for updates automatically

            // check for updates
            // setting result is responsibility of callee
            bt.checkServerUpdate();

            // await finish
            while (!updateTask.IsCompleted)
            {
                await Tools.SafeWait(200);
                if (!AICoreMod.Running)
                    shouldUpdate.TrySetCanceled();
            }
        });

        // Run bootstrapper
        Tools.SafeAsync(async () =>
        {
            // await start
            await updateTask;

            // do checks
            if (updateTask.IsCanceled || updateTask.IsFaulted)
            {
                LogTool.Error("Update process faulted, will not start server.");
                bootstrapDone.TrySetCanceled();
                return;
            }
            if (!updateTask.Result)
            {
                LogTool.Message($"You are running version {oldRelease}");
                bootstrapDone.TrySetResult(true);
                return;
            }

            // no issues, so bootstrap
            bt.BootstrapAsync();

            // await finish
            // we will cancel, callee will send sigint
            while (
                !bootstrapTask.IsCanceled && !bootstrapTask.IsFaulted && !bootstrapTask.IsCompleted
            )
            {
                await Tools.SafeWait(200);
                if (!AICoreMod.Running)
                    bootstrapDone.TrySetCanceled();
            }
        });

        // Do other things
        Tools.SafeAsync(async () =>
        {
            await bootstrapTask;

            // do checks
            if (bootstrapTask.IsCanceled || bootstrapTask.IsFaulted)
            {
                LogTool.Error("Bootstrap process faulted, will not start server.");
                return;
            }
            if (!bootstrapTask.Result)
            {
                LogTool.Message("Unexpected safe bootstrap result, will not start server.");
                return;
            }

            // this is just for testing
            // will depend on settings later
            if (AICoreMod.Running)
            {
                LogTool.Message("Starting AI Server!");
                AICoreMod.Server.Start();
            }
            else
                LogTool.Error("Did AICoreMod.Server fail to instantiate? Server not starting.");
        });
    }

    private static async Task<string> Download(
        ContentType content,
        Uri fileUrl,
        string userAgent = null,
        string destination = null
    )
    {
        string filePath = null;
        using var request = UnityWebRequest.Get(fileUrl);
        request.method = "GET";

        switch (content)
        {
            case ContentType.File:
#if DEBUG
                Debug.Assert(destination != null, "Destination cannot be null");
#endif

                if (Directory.Exists(destination))
                    filePath = Path.Combine(destination, Path.GetFileName(fileUrl.LocalPath));
                else
                {
                    if (File.Exists(destination))
                        File.Delete(destination);
                    filePath = destination;
                }

                break;
            case ContentType.String:
            default:
                break;
        }

        if (filePath == null && destination != null)
            throw new ArgumentException("filePath cannot be null. Does destination exist?");

        using DownloadHandler downloadHandler =
            destination != null ? new DownloadHandlerFile(filePath) : new DownloadHandlerBuffer();

        if (downloadHandler is DownloadHandlerFile fileHandler)
            fileHandler.removeFileOnAbort = true;

        request.downloadHandler = downloadHandler;
        if (userAgent != null)
            request.SetRequestHeader("User-Agent", userAgent);
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

    public async void BootstrapAsync()
    {
        var bootstrapUrl = new Uri(bootstrapString);
        var pythonUrl = new Uri(pythonString);
        var binPath = Directory.GetParent(pythonPath).FullName;
        string scriptContent = "";

        LogTool.Message("RWAI has begun bootstrapping!");
        ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Busy);

        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath))
            Directory.CreateDirectory(binPath);

        try
        {
            Task<string> pythonDownload = Download(ContentType.File, pythonUrl, null, pythonPath);
            Task<string> scriptDownload = Download(ContentType.String, bootstrapUrl);
            await Task.WhenAll(pythonDownload, scriptDownload);
            scriptContent = scriptDownload.Result;
        }
        catch (Exception ex)
        {
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            LogTool.Error("HTTP Error when downloading script or Python binary");
            LogTool.Error($"{ex.Message}");
            return;
        }

        // If OS is not Windows, make python executable
        if (platform != OSPlatform.Windows)
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
                ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
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
            bootstrapDone.TrySetException(ex);
            return;
        }

        if (bootstrapDone.Task.IsFaulted)
        {
            LogTool.Error("Unknown error occurred with bootstrap process!");
        }
        else if (bootstrapDone.Task.IsCanceled)
        {
            ProcessInterruptHelper.SendSigINT(bootstrapProcess);
        }
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
        {
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            bootstrapDone.TrySetResult(false);
        }
        else
            bootstrapDone.TrySetResult(true);
    }
}
