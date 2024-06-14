using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace AICore;

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
public static class BootstrapTool // : IDisposable
{
    public static CancellationTokenSource? onQuit;
    public static bool? internetAccess;
    public static bool? isConfigured;
    private const string releaseString =
        "https://api.github.com/repos/igoforth/RWAILib/releases/latest";
    private const string bootstrapString =
        "https://github.com/igoforth/RWAILib/releases/latest/download/bootstrap.py";
    private const string pythonString = "https://cosmo.zip/pub/cosmos/bin/python";
    private static string oldRelease = "_";
    private static string newRelease = "_";
    private static OSPlatform platform;
    private static Architecture arch;
    private static string? shellBin;
    private static string? modPath;
    private static string? pythonPath;
    private static string? llamaPath;
    // private static TaskCompletionSource<bool>? shouldUpdate;
    // private static TaskCompletionSource<bool>? bootstrapDone;
    private static Process? bootstrapProcess;
    private static readonly object lockObject = new();

    private enum ContentType
    {
        File,
        Content
    }

    public static void UpdateRunningState(bool enabled)
    {
        if (enabled)
            Start();
        else
            Stop();
    }

    public static void Init()
    {
        // hardware detection
        (platform, arch) = GetSystemInfo();
        SetGrpcOverrideLocation(platform, arch);

        // modPath related settings
        modPath = Path.Combine(
            Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
            "RWAI"
        );
        if (!Directory.Exists(modPath)) _ = Directory.CreateDirectory(modPath);

        // bin paths
        shellBin = platform == OSPlatform.Windows ? "powershell.exe" : "sh";
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

        // check if all files/folders are present
        isConfigured = CheckConfigured();
    }

    // check for internet access
    public static bool CheckInternet()
    {
        try
        {
            using var pingSender = new System.Net.NetworkInformation.Ping();
            var pingReply = pingSender.Send("dns.google");
            if (pingReply.Status == IPStatus.Success) return true;
        }
        catch (PingException)
        {
            return false;
        }
        return false;
    }

    public static void Reset()
    {
        lock (lockObject)
        {
            // Stop only if the bootstrap is ongoing
            if (onQuit != null) Stop();

            try
            {
                // DELETE RUNTIME FOLDER
                if (Directory.Exists(modPath)) Directory.Delete(modPath, true);  // Ensure recursive deletion
            }
            catch (IOException ex)
            {
                LogTool.Error($"Failed to delete runtime folder: {ex.Message}");
                // Consider how to handle failure: retry, abort, inform user, etc.
            }

            // Restart the bootstrap process
            Start();
        }
    }

    private static void Start()
    {
        lock (lockObject)
        {
            // Avoid starting if already running
            if (onQuit != null && !onQuit.IsCancellationRequested) return;

            // Reset the CancellationTokenSource when starting
            onQuit?.Dispose();  // Ensure previous token source is disposed if existing
            onQuit = new CancellationTokenSource();

            // Start the bootstrap process
            Run(onQuit.Token);
        }
    }

    private static void Stop()
    {
        lock (lockObject)
        {
            // Avoid stopping if not running or already completed
            if (onQuit == null || onQuit.IsCancellationRequested) return;

            // Properly dispose of our master CancellationTokenSource
            onQuit.Cancel();
            onQuit.Dispose();
            onQuit = null;
        }
    }

    private static bool CheckConfigured()
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
    private static async void CheckServerUpdate(TaskCompletionSource<bool> shouldUpdate)
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
            internetAccess = CheckInternet();
            if (internetAccess == false)
            {
                triggerUpdate = false;
                goto setUpdate;
            }

            // compare ".version" file with latest
            var releaseUrl = new Uri(releaseString);
            var userAgent = "igoforth/RWAILib";
            using (var reader = File.OpenText(versionPath))
            {
                Task<string> newReleaseT = Download(ContentType.Content, releaseUrl, userAgent);
                Task<string> oldReleaseT = reader.ReadToEndAsync();
                _ = await Task.WhenAll(newReleaseT, oldReleaseT).ConfigureAwait(false);

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
        catch (FileNotFoundException ex)
        {
            triggerUpdate = true;
#if RW15
            LogTool.Error($"File not found: {ex}");
#else
            LogTool.Error("File not found!");
#endif
        }
        catch (UriFormatException ex)
        {
            triggerUpdate = false;
#if RW15
            LogTool.Error($"Invalid URI format: {ex}");
#else
            LogTool.Error("File not found!");
#endif
        }
        catch (UnityWebRequestException ex)
        {
            triggerUpdate = false;
#if RW15
            LogTool.Error($"HTTP request error: {ex}");
#else
            LogTool.Error("File not found!");
#endif
        }
        catch (JsonException ex)
        {
            triggerUpdate = false;
#if RW15
            LogTool.Error($"JSON parsing error: {ex}");
#else
            LogTool.Error("File not found!");
#endif
        }
        catch (IOException ex)
        {
            triggerUpdate = false;
#if RW15
            LogTool.Error($"IO error: {ex}");
#else
            LogTool.Error("File not found!");
#endif
        }

    setUpdate:
        _ = triggerUpdate ? shouldUpdate.TrySetResult(true) : shouldUpdate.TrySetResult(false);
    }

    private static (OSPlatform, Architecture) GetSystemInfo()
    {
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OSPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? OSPlatform.Linux
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? OSPlatform.OSX
                    : throw new PlatformNotSupportedException("Unsupported OS platform.");

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => Architecture.X64,
            Architecture.X86 => Architecture.X86,
            Architecture.Arm64 => Architecture.Arm64,
            Architecture.Arm => throw new NotImplementedException(),
            _ => throw new PlatformNotSupportedException("Unsupported architecture.")
        };

        return (platform, arch);
    }

    private static void SetGrpcOverrideLocation(OSPlatform platform, Architecture arch)
    {
        // determine correct lib
        var libBaseDir = Path.GetFullPath(
            Path.Combine(Assembly.GetCallingAssembly().Location, @"..\..\Libraries\")
        );
        var libraryMapping = new Dictionary<
            (OSPlatform, Architecture),
            (string libraryName, string dstName)
        >
        {
            {
                (OSPlatform.Windows, Architecture.X64),
                ("grpc_csharp_ext.x64.dll", "grpc_csharp_ext.dll")
            },
            {
                (OSPlatform.Windows, Architecture.X86),
                ("grpc_csharp_ext.x86.dll", "grpc_csharp_ext.dll")
            },
            {
                (OSPlatform.Linux, Architecture.X64),
                ("libgrpc_csharp_ext.x64.so", "libgrpc_csharp_ext.so")
            },
            {
                (OSPlatform.OSX, Architecture.Arm64),
                ("libgrpc_csharp_ext.arm64.dylib", "libgrpc_csharp_ext.dylib")
            },
            {
                (OSPlatform.OSX, Architecture.X64),
                ("libgrpc_csharp_ext.x64.dylib", "libgrpc_csharp_ext.dylib")
            }
        };

        // set load path
        if (libraryMapping.TryGetValue((platform, arch), out var value))
        {
            string libraryName = value.libraryName;
            string dstName = value.dstName;

            // TODO: Fix, or something. gRPC doesn't listen
            Environment.SetEnvironmentVariable(
                "GRPC_CSHARP_EXT_OVERRIDE_LOCATION",
                Path.Combine(libBaseDir, libraryName)
            );

            // backup: copy lib into search path
            // `Fallback handler could not load library %USERPROFILE%/scoop/apps/steam/current/steamapps/common/RimWorld/RimWorldWin64_Data/Mono/grpc_csharp_ext.dll`
            var dllLoadDir = Path.Combine(Application.dataPath, "Mono");
            var dstPath = Path.Combine(dllLoadDir, dstName);
            if (!Directory.Exists(dllLoadDir))
                _ = Directory.CreateDirectory(dllLoadDir);
            if (!File.Exists(dstPath))
                File.Copy(Path.Combine(libBaseDir, libraryName), dstPath);
        }
        else throw new NotSupportedException("Unsupported OS and Architecture combination.");
    }

    private static void Run(CancellationToken token)
    {
        if (AICoreMod.Settings == null) return;

        // Everything required to establish runtime vars
        // like correct paths, libs, etc
        if (isConfigured == null) Init();

        // #pragma warning disable CA2000 // Dispose objects before losing scope
        //         BootstrapTool bt = new();
        // #pragma warning restore CA2000 // Dispose objects before losing scope
        var shouldUpdate = new TaskCompletionSource<bool>();
        var updateTask = shouldUpdate.Task;
        var bootstrapDone = new TaskCompletionSource<bool>();
        var bootstrapTask = bootstrapDone.Task;

        // Check for updates
        Tools.SafeAsync(async () =>
        {
            CheckServerUpdate(shouldUpdate);

            // await finish
            while (!updateTask.IsCompleted)
            {
                await Tools.SafeWait(200).ConfigureAwait(false);
                if (token.IsCancellationRequested || !AICoreMod.Running)
                {
                    _ = shouldUpdate.TrySetCanceled(token);
                    return;
                }
            }
        });

        // Run bootstrapper
        Tools.SafeAsync(async () =>
        {
            var result = await updateTask.ConfigureAwait(false);

            // error checks
            if (updateTask.IsCanceled || updateTask.IsFaulted)
            {
                LogTool.Error("Update process faulted, will not start server.");
                _ = bootstrapDone.TrySetCanceled();
                return;
            }

            // no internet
            if (!result)
            {
                LogTool.Message($"You are running version {oldRelease}");
                _ = bootstrapDone.TrySetResult(true);
                return;
            }

            BootstrapAsync(bootstrapDone, token);

            // await finish
            while (!bootstrapTask.IsCompleted)
            {
                await Tools.SafeWait(200).ConfigureAwait(false);
                if (token.IsCancellationRequested || !AICoreMod.Running)
                {
                    _ = bootstrapDone.TrySetCanceled(token);
                    return;
                }
            }
        });

        // Do other things
        Tools.SafeAsync(async () =>
        {
            var result = await bootstrapTask.ConfigureAwait(false);

            // error checks
            if (bootstrapTask.IsCanceled || bootstrapTask.IsFaulted)
            {
                LogTool.Error("Bootstrap process faulted, will not start server.");
                return;
            }

            // abnormal exit
            if (!result)
            {
                LogTool.Message("Unexpected safe bootstrap result, will not start server.");
                return;
            }

            isConfigured = CheckConfigured();

            if (isConfigured is not null and true)
            {
                AICoreMod.Server.UpdateRunningState(AICoreMod.Settings.Enabled);
                await AICoreMod.Client.UpdateRunningStateAsync(AICoreMod.Settings.Enabled).ConfigureAwait(false);
            }
            // bt.Dispose();
        });
    }

    private static async Task<string> Download(
        ContentType content,
        Uri fileUrl,
        string? userAgent = null,
        string? destination = null
    )
    {
        string? filePath = null;
        using var request = UnityWebRequest.Get(fileUrl);
        request.method = "GET";

        switch (content)
        {
            case ContentType.File:
#if DEBUG
                System.Diagnostics.Debug.Assert(destination != null, "Destination cannot be null");
#endif

                if (Directory.Exists(destination))
                    filePath = Path.Combine(destination, Path.GetFileName(fileUrl.LocalPath));
                else
                {
                    if (File.Exists(destination)) File.Delete(destination);
                    filePath = destination;
                }

                break;
            case ContentType.Content:
            default:
                break;
        }

        if (filePath == null && destination != null) throw new ArgumentException("filePath cannot be null. Does destination exist?");

        using DownloadHandler downloadHandler =
            destination != null ? new DownloadHandlerFile(filePath) : new DownloadHandlerBuffer();

        if (downloadHandler is DownloadHandlerFile fileHandler)
            fileHandler.removeFileOnAbort = true;

        request.downloadHandler = downloadHandler;
        if (userAgent != null)
            request.SetRequestHeader("User-Agent", userAgent);

        var asyncOperation = request.SendWebRequest();
        while (!asyncOperation.isDone && AICoreMod.Running)
            await Tools.SafeWait(200).ConfigureAwait(false);

        return request.error != null
            ? throw new UnityWebRequestException(request.error)
            : downloadHandler is DownloadHandlerFile
                ? ""
                : await Main.Perform(() =>
                    {
                        var result = downloadHandler.text;
                        return result;
                    })
                    .ConfigureAwait(false);
    }

    private static async void BootstrapAsync(TaskCompletionSource<bool> bootstrapDone, CancellationToken token)
    {
        if (bootstrapDone == null) throw new ArgumentException("BootstrapAsync: bootstrapDone task is null!");

        var bootstrapUrl = new Uri(bootstrapString);
        var pythonUrl = new Uri(pythonString);
        var binPath = Directory.GetParent(pythonPath).FullName;
        string scriptContent = "";

        LogTool.Message("RWAI has begun bootstrapping!");
        ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Busy);

        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath))
            _ = Directory.CreateDirectory(binPath);

        try
        {
            Task<string> pythonDownload = Download(ContentType.File, pythonUrl, null, pythonPath);
            Task<string> scriptDownload = Download(ContentType.Content, bootstrapUrl);
            _ = await Task.WhenAll(pythonDownload, scriptDownload).ConfigureAwait(false);
            scriptContent = scriptDownload.Result;
        }
        catch (UnityWebRequestException ex)
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
            _ = chmodProcess.Start();
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
            bootstrapProcess = new Process { StartInfo = pythonPSI, EnableRaisingEvents = true };

            bootstrapProcess.Exited += (sender, e) =>
            {
#if DEBUG
                var elapsedTime = Math.Round(
                    (bootstrapProcess.ExitTime - bootstrapProcess.StartTime).TotalMilliseconds
                );
                LogTool.Debug($"Exit time    : {bootstrapProcess.ExitTime}");
                LogTool.Debug($"Exit code    : {bootstrapProcess.ExitCode}");
                LogTool.Debug($"Elapsed time : {elapsedTime}");
#endif

                if (bootstrapProcess.ExitCode != 0)
                {
                    ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
                    bootstrapDone.TrySetResult(false);
                }
                else
                    bootstrapDone.TrySetResult(true);
            };

            bootstrapProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    // LogTool.Message(args.Data, "UISink");
#if DEBUG
                    LogTool.Message(args.Data);
#endif
            };

            bootstrapProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    // LogTool.Error(args.Data, "UISink");
                    LogTool.Error(args.Data);
            };

            _ = bootstrapProcess.Start();

            // Handling cancellation
            _ = token.Register(async () =>
            {
                if (!bootstrapProcess.HasExited)
                    ProcessInterruptHelper.SendSigINT(bootstrapProcess); // Interrupt the process if cancellation is requested
                await Tools.SafeWait(200).ConfigureAwait(false);
                if (!bootstrapProcess.HasExited)
                {
                    bootstrapProcess.Kill();
                    bootstrapProcess.Dispose();
                }
            });

            // Write the multiline script content to the process
            using (var sw = bootstrapProcess.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(scriptContent);
                }
            }

            // Read the output stream first and then wait.
            bootstrapProcess.BeginOutputReadLine();
            bootstrapProcess.BeginErrorReadLine();

            await Task.Run(() => bootstrapProcess.WaitForExit(), token).ConfigureAwait(false);

            // // Process completion missed, setting task result to false
            // if (!bootstrapDone.Task.IsCompleted)
            //     _ = bootstrapDone.TrySetResult(false);
        }
        catch (InvalidOperationException ex)
        {
            LogTool.Error($"Invalid operation error starting process: {ex}");
            _ = bootstrapDone.TrySetException(ex);
            return;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            LogTool.Error($"Win32 error starting process: {ex}");
            _ = bootstrapDone.TrySetException(ex);
            return;
        }
        catch (PlatformNotSupportedException ex)
        {
            LogTool.Error($"Platform not supported error starting process: {ex}");
            _ = bootstrapDone.TrySetException(ex);
            return;
        }
    }

    // public void Dispose()
    // {
    //     Dispose(true);
    //     GC.SuppressFinalize(this);
    // }

    // private static void Dispose(bool disposing)
    // {
    //     if (disposing)
    //     {
    //         onQuit.Cancel();
    //         onQuit.Dispose();
    //         bootstrapProcess?.Dispose();
    //     }
    // }
}
