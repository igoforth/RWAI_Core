using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace AICore;

// once configured, layout should look like this:
// root:
//   Darwin  - ~/Library/Application Support/RimWorld/RWAI/
//   Windows - %USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RWAI\
//   Linux   - ~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/RWAI/
// files:
// ./bin/curl (win curl.com)
// ./bin/llamafile (win llamafile.com)
// ./bin/python (win python.com)
// ./models/Phi-3-mini-128k-instruct.Q4_K_M.gguf
// ./AIServer.pyz
// ./.version
public static class BootstrapTool // : IDisposable
{
    public static CancellationTokenSource onQuit = new();
    public static bool Running => !onQuit.IsCancellationRequested;
    public static bool IsConfigured
    {
        get
        {
            if (isConfigured == null)
            {
                isConfigured = CheckConfigured();
                return (bool)isConfigured;
            }
            else if (AICoreMod.Settings != null && modelPreference != AICoreMod.Settings.ActiveModelSize)
            {
                modelPreference = AICoreMod.Settings.ActiveModelSize;
                isConfigured = CheckConfigured();
                return (bool)isConfigured;
            }
            return (bool)isConfigured;
        }
    }
    public static int PercentComplete;
    public static int VRAM;
    public static SemanticVersion ServerVersion => serverVersion ?? SemanticVersion.Parse("0.1.0-alpha");
    private const string releaseString =
        "https://api.github.com/repos/igoforth/RWAILib/releases/latest";
    private const string bootstrapString =
        "https://github.com/igoforth/RWAILib/releases/latest/download/bootstrap.py";
    private const string pythonString = "https://cosmo.zip/pub/cosmos/bin/python";
    private static AICoreSettings.ModelSize modelPreference = AICoreSettings.ModelSize.MINI;
    private static bool? isConfigured;
    private static SemanticVersion? serverVersion;
    private static bool? internetAccess;
    private static OSPlatform platform;
    private static Architecture arch;
    private static string? shellBin;
    private static string? modPath;
    private static string? pythonPath;
    private static string? scriptPath;
    private static string? llamaPath;
    private static readonly object lockObject = new();
    private static Process? bootstrapProcess;

    private enum ContentType
    {
        File,
        Content
    }

    public static void Init()
    {
        // hardware detection
        (platform, arch, VRAM) = GetSystemInfo();
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
        scriptPath = Path.Combine(modPath, "bootstrap.py");
        llamaPath = Path.Combine(
            modPath,
            "bin",
            platform == OSPlatform.Windows ? "llamafile.com" : "llamafile"
        );

        // check if all files/folders are present
        isConfigured = CheckConfigured();
    }


    // Check for internet access
    public static bool CheckInternet()
    {
        // Google DNS and Alibaba Cloud DNS
        string[] dnsAddresses = ["8.8.8.8", "100.100.2.136"];

        using var pingSender = new System.Net.NetworkInformation.Ping();
        foreach (var address in dnsAddresses)
        {
            try
            {
                var pingReply = pingSender.Send(address);
                if (pingReply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    return true;
            }
            catch (PingException)
            {
                // Continue to the next address
            }
        }

        return false;
    }

    public static void UpdateRunningState(bool enabled)
    {
        if (enabled) Start();
        else Stop();
    }

    public static void Reset()
    {
        lock (lockObject)
        {
            // Stop only if the bootstrap is ongoing
            if (Running) Stop();

            // Stop client and server
            JobClient.UpdateRunningState(false);
            ServerManager.UpdateRunningState(false);

            // DELETE RUNTIME FOLDER
            try
            {
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


    public static bool CheckConfigured()
    {
        var directoryPathList = new[]
        {
            Path.Combine(modPath, "bin"),
            Path.Combine(modPath, "models")
        };

        // Check if directories exist
        foreach (var filePath in directoryPathList)
            if (!Directory.Exists(filePath))
                return false;

        var filePathList = new[]
        {
            llamaPath,
            pythonPath,
            Path.Combine(modPath, "AIServer.pyz"),
            Path.Combine(modPath, ".version")
        };

        // Check if files exist
        foreach (var filePath in filePathList)
            if (!File.Exists(filePath))
                return false;

        // Get current version
        serverVersion = ParseVersion(File.ReadAllText(Path.Combine(modPath, ".version")));

        // Get model size details from settings
        var modelsPath = Path.Combine(modPath, "models");
        var (_, size) = AICoreSettings.AvailableModelSizes.TryGetValue(modelPreference);
        ulong modelSizeBytes = ((ulong)size) * 1000 * 1000;

        // temporary fixes
#pragma warning disable CA1308 // Normalize strings to uppercase
        var tempModelName = modelPreference.ToString().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
        if (tempModelName == "small") tempModelName = "medium"; // temporary because there is no Phi-3-small gguf yet
        // if (tempModelName == "any") tempModelName = "mini"; // If wildcard "any" model name, check for mini

        // Get all files in models directory
        var files = Directory.GetFiles(modelsPath);
        if (files.Length == 0)
            return false;
        // if "custom" and directory is not empty
        else if (tempModelName == "custom")
            return true;

        // Get files that match the model size name pattern
        files = Directory.GetFiles(modelsPath, "*" + tempModelName + "*");
        if (files.Length == 0)
            return false;

        // Ensure each file in "files" is at least above modelSizeBytes
        foreach (var file in files)
        {
            FileInfo fileInfo = new(file);
            if (fileInfo.Length < (long)modelSizeBytes)
                return false; // If any file is below the size threshold, return false
        }

        LogTool.Message($"You are running version {serverVersion}");
        return true; // All checks passed
    }

    private static void Start()
    {
        lock (lockObject)
        {
            // Avoid starting if already running
            if (Running && ServerManager.currentServerStatusEnum == ServerManager.ServerStatus.Busy) return;

            // Reset the CancellationTokenSource when starting
            onQuit.Dispose();
            onQuit = new CancellationTokenSource();

            // Turn off client and server so they don't interrupt update
            AICoreMod.Client.UpdateRunningStateAsync(false).Wait();
            ServerManager.UpdateRunningState(false);

            // Start the bootstrap process
            Run(onQuit.Token);
        }
    }

    private static void Stop()
    {
        lock (lockObject)
        {
            // Avoid stopping if cancellation is not requested
            if (!Running) return;

            // Properly dispose of our master CancellationTokenSource
            onQuit.Cancel();
            onQuit.Dispose();
        }
    }

    private static (OSPlatform, Architecture, int) GetSystemInfo()
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

        var vram = GetVRAM();

        return (platform, arch, vram);
    }

    private static SemanticVersion ParseVersion(string version)
    {
        version = version.Trim();
        // Strip "v" from the beginning of the version if it exists
        if (version.StartsWith("v"))
        {
            version = version[1..];
        }
        return SemanticVersion.Parse(version);
    }

    private static void SetGrpcOverrideLocation(OSPlatform platform, Architecture arch)
    {
        // determine correct lib
        var libBaseDir = Path.GetFullPath(Path.Combine(AICoreMod.self?.Content.RootDir, "Libraries"));
        var libraryMapping = new Dictionary<
            (OSPlatform, Architecture),
            (string libraryName, string dstName)
        >
        {
            {
                (OSPlatform.Windows, Architecture.X64),
                ("grpc_csharp_ext.x64.dll.gz", "grpc_csharp_ext.dll")
            },
            {
                (OSPlatform.Windows, Architecture.X86),
                ("grpc_csharp_ext.x86.dll.gz", "grpc_csharp_ext.dll")
            },
            {
                (OSPlatform.Linux, Architecture.X64),
                ("libgrpc_csharp_ext.x64.so.gz", "libgrpc_csharp_ext.so")
            },
            {
                (OSPlatform.Linux, Architecture.Arm64),
                ("libgrpc_csharp_ext.arm64.so.gz", "libgrpc_csharp_ext.so")
            },
            {
                (OSPlatform.OSX, Architecture.Arm64),
                ("libgrpc_csharp_ext.arm64.dylib.gz", "libgrpc_csharp_ext.dylib")
            },
            {
                (OSPlatform.OSX, Architecture.X64),
                ("libgrpc_csharp_ext.x64.dylib.gz", "libgrpc_csharp_ext.dylib")
            }
        };

        // set load path
        if (libraryMapping.TryGetValue((platform, arch), out var value))
        {
            string libraryName = value.libraryName;
            string dstName = value.dstName;

            var libPath = Path.Combine(libBaseDir, libraryName);
            var dllLoadDir = Path.Combine(Application.dataPath, "Mono");
            var dstPath = Path.Combine(dllLoadDir, dstName);

            if (!Directory.Exists(dllLoadDir))
                _ = Directory.CreateDirectory(dllLoadDir);

            // backup: copy lib into search path
            // `Fallback handler could not load library %USERPROFILE%/scoop/apps/steam/current/steamapps/common/RimWorld/RimWorldWin64_Data/Mono/grpc_csharp_ext.dll`
            // Check if the destination file already exists
            if (!File.Exists(dstPath))
            {
                using (FileStream gzStream = new(libPath, FileMode.Open, FileAccess.Read))
                {
                    using (GZipStream decompressionStream = new(gzStream, CompressionMode.Decompress))
                    {
                        using (FileStream outputFileStream = new(dstPath, FileMode.Create, FileAccess.Write))
                        {
                            decompressionStream.CopyTo(outputFileStream);
                        }
                    }
                }
            }

            // TODO: Fix, or something. gRPC doesn't listen
            Environment.SetEnvironmentVariable("GRPC_CSHARP_EXT_OVERRIDE_LOCATION", dstPath);
        }
        else throw new NotSupportedException("Unsupported OS and Architecture combination.");
    }

    private static void Run(CancellationToken token)
    {
        // This should never be hit, because Settings is always instantiated in Main with Init()
        if (AICoreMod.Settings == null) return;

        // I'm commenting this out because Init() is always called in Main
        // if (isConfigured == null) Init();

        try
        {
            // Start the asynchronous operation for checking and applying updates,
            // then bootstrapping, and finally additional configurations.
            _ = Task.Run(async () => await ManageBootstrapAsync(token).ConfigureAwait(false), token);
        }
        catch (Exception ex)
        {
            LogTool.Error("Error during bootstrap process:" + ex.Message);
        }
    }


    private static int GetVRAM()
    {
        // Construct the full path to the Player.log file
        var playerLogPath = Path.Combine(Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(), "Player.log");
        string tempPath = Path.GetTempFileName(); // Creates a temporary file

        // Verify that the file exists before attempting to open it
        if (!File.Exists(playerLogPath))
        {
            LogTool.Error("Player log file does not exist: " + playerLogPath);
            return 0; // Return 0 if the file doesn't exist
        }

        // Create a regex pattern to match VRAM information
        var regex = new Regex(@"^\s+VRAM:\s+(\d+) MB$", RegexOptions.Multiline);

        try
        {
            File.Copy(playerLogPath, tempPath, true); // Copy the original file to a temp file, overwriting it if it already exists
            // Open the file with read access and allow other processes to read from it too
            using (StreamReader reader = new(tempPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null) // Read line by line
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        // If a match is found, parse the VRAM value and return it
                        return int.Parse(match.Groups[1].Value);
                    }
                }
            }
        }
        catch (IOException ex)
        {
            // Log or handle the exception as needed
            LogTool.Error("An error occurred while reading the VRAM from the log file: " + ex.Message);
        }
        finally
        {
            File.Delete(tempPath); // Clean up the temp file
        }

        // Return 0 if no VRAM information is found or if an error occurs
        return 0;
    }

    private static async Task ManageBootstrapAsync(CancellationToken token)
    {
        // Check for updates and decide if an update should be applied
        var (update, version) = await CheckServerUpdateAsync().ConfigureAwait(false);
        if (token.IsCancellationRequested) return;
        if (!update && isConfigured == true)
        {
            ServerManager.UpdateRunningState(AICoreMod.Settings!.Enabled);
            await AICoreMod.Client.UpdateRunningStateAsync(AICoreMod.Settings!.Enabled).ConfigureAwait(false);
            return;
        }
        else
        {
            if (version == null) LogTool.Message("RWAI files haven't been found!");
            else LogTool.Message($"RWAI has found a new version: {version}");
        }

        LogTool.Message("RWAI has begun bootstrapping!");

        string? pythonPath;
        string? scriptContent;

        // Fetch the bootstrap script required to update
        var result = await FetchBootstrapperAsync().ConfigureAwait(false);
        if (!result.HasValue || token.IsCancellationRequested)
        {
            LogTool.Error("Update failed or was cancelled.");
            return;
        }
        else (pythonPath, scriptContent) = result.Value;

        // Initialize placeholders in script
        scriptContent = InitializePlaceholders(scriptContent);
        File.WriteAllText(scriptPath, scriptContent);

        // Run bootstrapper only if the update was successful or not needed
        bool bootstrapResult = PerformBootstrap(pythonPath, scriptContent, token);
        // If the bootstrap or update failed, do not attempt to start
        if (!bootstrapResult && token.IsCancellationRequested)
        {
            LogTool.Error("Bootstrap process failed or was cancelled in a way that made it close unexpectedly.");
            if (bootstrapProcess != null)
            {
                bootstrapProcess.Dispose();
                bootstrapProcess = null;
            }
            return;
        }

        if (bootstrapProcess != null)
        {
            bootstrapProcess.Dispose();
            bootstrapProcess = null;
        }

        // If Enabled is set in settings, server should automatically start
        // isConfigured is updated to reflect the new state
        // so that any extra checks not performed at startup are true
        // and if isConfigured is false, it serves as a safety check
        // where even if the bootstrap process returned exit code 0
        // the safety will not be risked
        if (AICoreMod.Settings!.Enabled && (isConfigured = CheckConfigured()) == true)
        {
            ServerManager.UpdateRunningState(AICoreMod.Settings.Enabled);
            await AICoreMod.Client.UpdateRunningStateAsync(AICoreMod.Settings.Enabled).ConfigureAwait(false);
        }
    }

    // compare github api against pinned "./.version"
    private static async Task<(bool shouldUpdate, SemanticVersion? version)> CheckServerUpdateAsync()
    {
        SemanticVersion? oldVersion = null;

        try
        {
            // check for ".version" file
            var versionPath = Path.Combine(modPath, ".version");
            if (!File.Exists(versionPath)) return (true, oldVersion);
            oldVersion = serverVersion ?? ParseVersion(File.ReadAllText(versionPath));

            // check for internet
            internetAccess = CheckInternet();
            if (internetAccess == false) return (false, oldVersion);

            // compare ".version" file with latest
            var releaseUrl = new Uri(releaseString);
            var userAgent = "igoforth/RWAILib";

            string apiContent = await Download(ContentType.Content, releaseUrl, userAgent).ConfigureAwait(false);

            var json = JObject.Parse(apiContent);
            SemanticVersion newVersion = ParseVersion((string)json["tag_name"]);
            return newVersion > oldVersion ? ((bool shouldUpdate, SemanticVersion version))(true, newVersion) : ((bool shouldUpdate, SemanticVersion version))(false, oldVersion);
        }
        catch (FileNotFoundException ex)
        {
            LogTool.Warning($"File not found: {ex}");
            return (true, oldVersion);
        }
        catch (UriFormatException ex)
        {
            LogTool.Error($"Invalid URI format: {ex}");
            return (false, oldVersion);
        }
        catch (UnityWebRequestException ex)
        {
            LogTool.Error($"HTTP request error: {ex}");
            return (false, oldVersion);
        }
        catch (JsonException ex)
        {
            LogTool.Error($"JSON parsing error: {ex}");
            return (false, oldVersion);
        }
        catch (IOException ex)
        {
            LogTool.Error($"IO error: {ex}");
            return (false, oldVersion);
        }
    }

    private static async Task<(string binPath, string scriptContent)?> FetchBootstrapperAsync()
    {
        if (pythonPath == null) return null;

        var bootstrapUrl = new Uri(bootstrapString);
        var pythonUrl = new Uri(pythonString);
        var binPath = Directory.GetParent(pythonPath).FullName;

        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath)) _ = Directory.CreateDirectory(binPath);

        try
        {
            Task<string> pythonDownload = Download(ContentType.File, pythonUrl, null, pythonPath);
            Task<string> scriptDownload = Download(ContentType.Content, bootstrapUrl);
            var result = await Task.WhenAll(pythonDownload, scriptDownload).ConfigureAwait(false);
            return (pythonPath, result[1]);
        }
        catch (UnityWebRequestException ex)
        {
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            LogTool.Error("HTTP Error when downloading script or Python binary");
            LogTool.Error($"{ex.Message}");
            return null;
        }
    }

    private static string InitializePlaceholders(string script)
    {
        // Modify language placeholder
        string placeholder = "PLACEHOLDER_STRING_LANGUAGE";
        string languageValue = LanguageMapping.FindKeyByValue(UpdateLanguage.activeLanguage);
        script = script.Replace(placeholder, languageValue);

        // Modify model size placeholder
        placeholder = "PLACEHOLDER_STRING_MODELSIZE";
        string modelSize = AICoreMod.Settings!.ActiveModelSize.ToString();
        script = script.Replace(placeholder, modelSize);

        return script;
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

    private static bool PerformBootstrap(string pythonPath, string scriptContent, CancellationToken token)
    {
        try
        {
            // If OS is not Windows, make python executable
            if (platform != OSPlatform.Windows)
            {
                using (var chmodProcess = new Process
                {
                    StartInfo = {
                    FileName = "chmod",
                    Arguments = $"+x \"{pythonPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
                })
                {
                    chmodProcess.Start();
                    chmodProcess.WaitForExit();
                    if (chmodProcess.ExitCode != 0)
                    {
                        ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
                        LogTool.Error("Failed to set executable permission on Python binary.");
                        return false;
                    }
                }
            }

            using (bootstrapProcess = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = {
                    FileName = shellBin,
                    Arguments = $"bin/python -u bootstrap.py 2>&1",
                    WorkingDirectory = modPath,
                    // RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    // RedirectStandardError = true,
                    // StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                bootstrapProcess.Start();
                ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Busy);
#if DEBUG
                LogTool.Debug("Process started.");
#endif
                // Handling cancellation
                using (var registration = token.Register(() =>
                {
                    if (!bootstrapProcess.HasExited)
                    {
#if DEBUG
                        LogTool.Debug("Sending SIGINT to process.");
#endif
                        ProcessInterruptHelper.SendSigINT(bootstrapProcess);
                    }
                }))
                {
                    using (StreamReader reader = bootstrapProcess.StandardOutput)
                    {
                        string outputLine;
                        // Read output line by line synchronously
                        while ((outputLine = reader.ReadLine()) != null)
                        {
#if DEBUG
                            LogTool.Debug($"Output received: {outputLine}");
#endif
                            var success = int.TryParse(outputLine, out PercentComplete);
                            if (success)
                                ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Busy);
                            else
                            {

                            }
                        }
                    }

#if DEBUG
                    LogTool.Debug("Finished reading output stream.");
#endif

                    bootstrapProcess.WaitForExit();

                    serverVersion = ParseVersion(File.ReadAllText(Path.Combine(modPath, ".version")));

#if DEBUG
                    var elapsedTime = Math.Round(
                        (bootstrapProcess.ExitTime - bootstrapProcess.StartTime).TotalMilliseconds
                    );
                    LogTool.Debug($"Exit time    : {bootstrapProcess.ExitTime}");
                    LogTool.Debug($"Exit code    : {bootstrapProcess.ExitCode}");
                    LogTool.Debug($"Elapsed time : {elapsedTime}");
#endif

                    if (bootstrapProcess.ExitCode is not 0 and not 1)
                    {
                        LogTool.Warning($"Bootstrap process exited with non-zero code: {bootstrapProcess.ExitCode}");
                        ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
                    }
                    else
                    {
                        ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Offline);
                    }
                    return bootstrapProcess.ExitCode is 0 or 1;
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            LogTool.Error($"Invalid operation error starting process: {ex}");
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            LogTool.Error($"Win32 error starting process: {ex}");
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            return false;
        }
        catch (PlatformNotSupportedException ex)
        {
            LogTool.Error($"Platform not supported error starting process: {ex}");
            ServerManager.UpdateServerStatus(ServerManager.ServerStatus.Error);
            return false;
        }
    }
}
