using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Verse;

namespace AICore;

public class BootstrapTool
{
    public static bool DEBUG = true;
    public static readonly string configPath = Path.Combine(
        GenFilePaths.ConfigFolderPath,
        "RWAILib"
    );
    private static readonly string binPath = Path.Combine(configPath, "bin");
    private const string bootstrapUrl =
        "https://raw.githubusercontent.com/igoforth/RWAILib/AIServer/Bootstrap.py";
    private const string pythonUrl = "https://cosmo.zip/pub/cosmos/bin/python";
    private static readonly string pythonPath = Path.Combine(
        binPath,
        Environment.OSVersion.Platform == PlatformID.Win32NT ? "python.com" : "python"
    );

    // Bootstrap process information
    private Process bootstrapProcess;
    private static TaskCompletionSource<bool> eventHandled;

    public static async Task Run()
    {
        // Create the bin directory if it doesn't exist
        if (!Directory.Exists(binPath))
        {
            Directory.CreateDirectory(binPath);
        }

        try
        {
            // Create download jobs
            var pythonDownload = DownloadFile(pythonUrl, pythonPath);
            var scriptDownload = DownloadString(bootstrapUrl);
            await Task.WhenAll(pythonDownload, scriptDownload);

            // If OS is not Windows, make python executable
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x {pythonPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process chmodProcess = new Process { StartInfo = psi })
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
            string scriptContent = await scriptDownload;

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

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = pythonBin,
            WorkingDirectory = configPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (bootstrapProcess = new Process { StartInfo = psi })
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
