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
public sealed class BootstrapTool : IDisposable
{
    public static bool internetAccess;
    public static bool isConfigured = true;
    public static Action preInit = () =>
    {
        GetSystemInfo();
        SetGrpcOverrideLocation();
    };
    private const string releaseString =
        "https://api.github.com/repos/igoforth/RWAILib/releases/latest";
    private const string bootstrapString =
        "https://github.com/igoforth/RWAILib/releases/latest/download/bootstrap.py";
    private const string pythonString = "https://cosmo.zip/pub/cosmos/bin/python";
    private static string oldRelease = "_";
    private static string newRelease = "_";
    private static OSPlatform? platform;
    private static Architecture? arch;
    private static string? shellBin;
    private static string? modPath;
    private static string? pythonPath;
    private static string? llamaPath;
    private static TaskCompletionSource<bool>? shouldUpdate;
    private static TaskCompletionSource<bool>? bootstrapDone;
    private Process? bootstrapProcess;

    private enum ContentType
    {
        File,
        Content
    }

    private BootstrapTool()
    {
        if (platform == null || arch == null)
        {
            preInit();
        }

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
        isConfigured = CheckConfigured();
    }

    private static bool CheckConfigured()
    {
        var directory_path_list = new[]
        {
            Path.Combine(modPath, "bin"),
            Path.Combine(modPath, "models")
        };

        foreach (var file_path in directory_path_list)
            if (!Directory.Exists(file_path)) return false;

        // TODO: do something smarter to detect models available
        var files = Directory.GetFiles(Path.Combine(modPath, "models"));
        if (files.Length == 0) return false;

        var file_path_list = new[]
        {
            llamaPath,
            pythonPath,
            Path.Combine(modPath, "AIServer.pyz"),
            Path.Combine(modPath, ".version")
        };

        foreach (var file_path in file_path_list)
            if (!File.Exists(file_path)) return false;

        return true;
    }

    // compare github api against pinned "./.version"
    private static async void CheckServerUpdate()
    {
        if (shouldUpdate == null) return; // task initiated by caller

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
            LogTool.Error($"File not found: {ex}");
        }
        catch (UriFormatException ex)
        {
            triggerUpdate = false;
            LogTool.Error($"Invalid URI format: {ex}");
        }
        catch (UnityWebRequestException ex)
        {
            triggerUpdate = false;
            LogTool.Error($"HTTP request error: {ex}");
        }
        catch (JsonException ex)
        {
            triggerUpdate = false;
            LogTool.Error($"JSON parsing error: {ex}");
        }
        catch (IOException ex)
        {
            triggerUpdate = false;
            LogTool.Error($"IO error: {ex}");
        }

    setUpdate:
        _ = triggerUpdate ? shouldUpdate.TrySetResult(true) : shouldUpdate.TrySetResult(false);
    }

    // check for internet access
    public static bool CheckInternet()
    {
        try
        {
            using var pingSender = new System.Net.NetworkInformation.Ping();
            var pingReply = pingSender.Send("dns.google");
            if (pingReply.Status == IPStatus.Success)
            {
                return true;
            }
        }
        catch (PingException)
        {
            return false;
        }
        return false;
    }

    private static void GetSystemInfo()
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
            Architecture.Arm => throw new NotImplementedException(),
            _ => throw new PlatformNotSupportedException("Unsupported architecture.")
        };
    }

    private static void SetGrpcOverrideLocation()
    {
        if (platform == null || arch == null)
        {
            return;
        }

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
        if (libraryMapping.TryGetValue(((OSPlatform, Architecture))(platform, arch), out var value))
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
            {
                _ = Directory.CreateDirectory(dllLoadDir);
            }

            if (!File.Exists(dstPath))
            {
                File.Copy(Path.Combine(libBaseDir, libraryName), dstPath);
            }
        }
        else
        {
            throw new NotSupportedException("Unsupported OS and Architecture combination.");
        }
    }

    public static void Run()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        BootstrapTool bt = new();
#pragma warning restore CA2000 // Dispose objects before losing scope
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

            /* Unmerged change from project '1.4 (net472)'
            Before:
                        bt.CheckServerUpdate();
            After:
                        BootstrapTool.CheckServerUpdate();
            */
            CheckServerUpdate();

            // await finish
            while (!updateTask.IsCompleted)
            {
                await Tools.SafeWait(200).ConfigureAwait(false);
                if (!AICoreMod.Running)
                {
                    _ = shouldUpdate.TrySetCanceled();
                }
            }
        });

        // Run bootstrapper
        Tools.SafeAsync(async () =>
        {

            // await start
            _ = await updateTask.ConfigureAwait(false);

            // do checks
            if (updateTask.IsCanceled || updateTask.IsFaulted)
            {
                LogTool.Error("Update process faulted, will not start server.");
                _ = bootstrapDone.TrySetCanceled();
                return;
            }
            if (!await updateTask.ConfigureAwait(false))
            {
                LogTool.Message($"You are running version {oldRelease}");
                _ = bootstrapDone.TrySetResult(true);
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
                await Tools.SafeWait(200).ConfigureAwait(false);
                if (!AICoreMod.Running)
                    _ = bootstrapDone.TrySetCanceled();
            }
        });

        // Do other things
        Tools.SafeAsync(async () =>
        {
            _ = await bootstrapTask.ConfigureAwait(false);

            // do checks
            if (bootstrapTask.IsCanceled || bootstrapTask.IsFaulted)
            {
                LogTool.Error("Bootstrap process faulted, will not start server.");
                return;
            }
            if (!await bootstrapTask.ConfigureAwait(false))
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
            {
                LogTool.Error("Did AICoreMod.Server fail to instantiate? Server not starting.");
            }

            bt.Dispose();
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
                {
                    filePath = Path.Combine(destination, Path.GetFileName(fileUrl.LocalPath));
                }
                else
                {
                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }

                    filePath = destination;
                }

                break;
            case ContentType.Content:
            default:
                break;
        }

        if (filePath == null && destination != null)
        {
            throw new ArgumentException("filePath cannot be null. Does destination exist?");
        }

        using DownloadHandler downloadHandler =
            destination != null ? new DownloadHandlerFile(filePath) : new DownloadHandlerBuffer();

        if (downloadHandler is DownloadHandlerFile fileHandler)
        {
            fileHandler.removeFileOnAbort = true;
        }

        request.downloadHandler = downloadHandler;
        if (userAgent != null)
        {
            request.SetRequestHeader("User-Agent", userAgent);
        }

        var asyncOperation = request.SendWebRequest();
        while (!asyncOperation.isDone && AICoreMod.Running)
        {
            await Tools.SafeWait(200).ConfigureAwait(false);
        }

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

    public async void BootstrapAsync()
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
        {
            _ = Directory.CreateDirectory(binPath);
        }

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
            bootstrapProcess = new Process
            {
                StartInfo = pythonPSI,
                EnableRaisingEvents = true
            };

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

            _ = bootstrapProcess.Start();

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

            await Task.Run(() => bootstrapProcess.WaitForExit()).ConfigureAwait(false);

            // Process completion missed, setting task result to false
            if (!bootstrapDone.Task.IsCompleted)
            {
                _ = bootstrapDone.TrySetResult(false);
            }
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
        if (bootstrapDone.Task.IsFaulted)
        {
            // true negative
            LogTool.Error("Unknown error occurred with bootstrap process!");
        }
        else if (bootstrapDone.Task.IsCanceled)
        {
            // true negative
            ProcessInterruptHelper.SendSigINT(bootstrapProcess);
        }
    }

    // Handle Exited event and display process information.
    private void ProcessExited(object sender, EventArgs e)
    {
        if (bootstrapProcess == null || bootstrapDone == null)
        {
            return;
        }

#if DEBUG
        var elapsedTime = Math.Round((bootstrapProcess.ExitTime - bootstrapProcess.StartTime).TotalMilliseconds);
        LogTool.Debug($"Exit time    : {bootstrapProcess.ExitTime}");
        LogTool.Debug($"Exit code    : {bootstrapProcess.ExitCode}");
        LogTool.Debug($"Elapsed time : {elapsedTime}"
        );
#endif
        if (bootstrapProcess.ExitCode != 0)
        {
            // true negative
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            bootstrapDone.TrySetResult(false);
        }
        else
        {
            // true positive
            bootstrapDone.TrySetResult(true);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            bootstrapProcess?.Dispose();
            _ = (shouldUpdate?.TrySetCanceled());
            _ = (bootstrapDone?.TrySetCanceled());
        }
    }

}
