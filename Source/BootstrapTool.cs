using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Verse;

namespace AICore;

// once configured, layout should look like this:
// root:
//   Darwin  - ~/Library/Application Support/RimWorld/RWAI/
//   Windows - %APPDATA%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RWAI\
//   Linux   - $HOME/.config/unity3d/Ludeon Studios/RimWorld/RWAI/
// files:
// ./bin/llamafile (win llamafile.com)
// ./bin/python (win python.com)
// ./models/Phi-3-mini-128k-instruct.Q4_K_M.gguf
// ./AIServer.zip
// ./.version
public class BootstrapTool
{
    public static bool DEBUG = true;
    public static bool isConfigured = checkConfigured();
    public static bool internetAccess = false;
    public static bool hasServerUpdate = checkServerUpdate();
    private static readonly PlatformID platform = Environment.OSVersion.Platform;
    private static readonly string modConfigPath = Path.Combine(
        GenFilePaths.ConfigFolderPath,
        "RWAI"
    );
    private static readonly string pythonPath = Path.Combine(
        modConfigPath,
        "bin",
        platform == PlatformID.Win32NT ? "python.com" : "python"
    );

    private Process bootstrapProcess;
    private static TaskCompletionSource<bool> eventHandled;

    private static bool checkConfigured()
    {
        var file_path_list = new[]
        {
            Path.Combine(
                modConfigPath,
                "bin",
                platform == PlatformID.Win32NT ? "llamafile.com" : "python"
            ),
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
        {
            return false;
        }

        // compare ".version" file with latest
        var reader = File.OpenText(versionPath);
        const string releaseUrl = "https://api.github.com/repos/igoforth/RWAILib/releases/latest";
        var newRelease = "";
        var oldRelease = "";

        // compare online version
        Tools.SafeAsync(async () =>
        {
            Task<string> newReleaseT = DownloadString(releaseUrl);
            Task<string> oldReleaseT = reader.ReadToEndAsync();
            await Task.WhenAll(newReleaseT, oldReleaseT);
        });

        reader.Close();
        dynamic json = JObject.Parse(newRelease);
        return json.tag_name != oldRelease;
    }

    public static bool checkInternet()
    {
        try
        {
            // check for internet access
            var pingSender = new Ping();
            PingReply pingReply = pingSender.Send("dns.google");
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

    public static async Task Run()
    {
        const string bootstrapUrl =
            "https://github.com/igoforth/RWAILib/releases/latest/download/Bootstrap.py";
        const string pythonUrl = "https://cosmo.zip/pub/cosmos/bin/python";
        var binPath = Directory.GetParent(pythonPath).FullName;

        LogTool.Message("RWAI has begun bootstrapping.");

        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath))
        {
            Directory.CreateDirectory(binPath);
        }

        try
        {
            // Create download jobs
            Task pythonDownload = DownloadFile(pythonUrl, pythonPath);
            Task<string> scriptDownload = DownloadString(bootstrapUrl);
            await Task.WhenAll(pythonDownload, scriptDownload);

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

                using (Process chmodProcess = new Process { StartInfo = chmodPSI })
                {
                    chmodProcess.Start();
                    chmodProcess.WaitForExit();

                    if (chmodProcess.ExitCode != 0)
                    {
                        LogTool.Error("Failed to set executable permission on Python binary.");
                        return;
                    }
                }
            }

            // Get the script content
            string scriptContent = scriptDownload.Result;
            if (string.IsNullOrEmpty(scriptContent))
            {
                LogTool.Error("Failed to download the bootstrap script.");
                return;
            }

            // Run the bootstrap process
            BootstrapTool bt = new BootstrapTool();
            await bt.Bootstrap(pythonPath, scriptContent);
        }
        catch (Exception ex)
        {
            LogTool.Error($"An error occurred during download or processing: {ex.Message}");
        }
    }

    public static async Task<Stream> Download(string fileUrl)
    {
        using (HttpClient client = new HttpClient())
        {
            // Download the file
            using (var response = await client.GetAsync(fileUrl))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync();
            }
        }
    }

    public static async Task<string> DownloadString(string fileUrl)
    {
        using (Stream stream = await Download(fileUrl))
        {
            if (stream == null)
            {
                LogTool.Error("Download failed.");
                return null;
            }

            using (StreamReader reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }

    public static async Task DownloadFile(string fileUrl, string destinationPath)
    {
        // Ensure destinationPath is not directory
        if (Directory.Exists(destinationPath))
        {
            LogTool.Error("Destination path is a directory.");
            return;
        }
        // Ensure the destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

        // Download the file
        using (Stream stream = await Download(fileUrl))
        {
            if (stream == null)
            {
                LogTool.Error("Download failed.");
                return;
            }

            using (FileStream fs = File.Create(destinationPath))
            {
                await stream.CopyToAsync(fs);
            }
        }
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

        using (bootstrapProcess = new Process { StartInfo = pythonPSI })
        {
            try
            {
                bootstrapProcess.EnableRaisingEvents = true;
                bootstrapProcess.Exited += new EventHandler(ProcessExited);
                bootstrapProcess.Start();

                // Write the multiline script content to the process
                using (StreamWriter sw = bootstrapProcess.StandardInput)
                {
                    sw.WriteLine(scriptContent);
                }

                // Read the output stream first and then wait.
                bootstrapProcess.BeginOutputReadLine();
                bootstrapProcess.BeginErrorReadLine();
                if (DEBUG)
                {
                    bootstrapProcess.OutputDataReceived += (sender, args) =>
                        LogTool.Message(args.Data);
                    bootstrapProcess.ErrorDataReceived += (sender, args) =>
                        LogTool.Error(args.Data);
                }
                else
                {
                    // Redirect output to null
                    bootstrapProcess.OutputDataReceived += (sender, args) => { };
                    bootstrapProcess.ErrorDataReceived += (sender, args) => { };
                }
            }
            catch (Exception ex)
            {
                LogTool.Error($"Error starting process: {ex}");
                return;
            }

            await Task.WhenAny(eventHandled.Task);
        }
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
