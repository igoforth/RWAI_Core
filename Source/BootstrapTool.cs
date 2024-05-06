using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
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
    private const string bootstrapUrl = "https://github.com/igoforth/RWAILib/AIServer/Server.py";
    private const string cosmosUrl = "https://cosmo.zip/pub/cosmos/";
    private const string bashUrl = cosmosUrl + "bin/bash";
    private static readonly string bashPath = Path.Combine(binPath, "bash.com");
    private const string pythonUrl = cosmosUrl + "bin/pypack1";
    private static readonly string pythonPath = Path.Combine(binPath, "python");
    private const string webZipUrl = cosmosUrl + "zip/web.zip";
    private static readonly string webZipPath = Path.Combine(Path.GetTempPath(), "web.zip");
    private const string gitZipUrl = cosmosUrl + "zip/git.zip";
    private static readonly string gitZipPath = Path.Combine(Path.GetTempPath(), "git.zip");
    private static readonly string[] cmdArgs =
    {
        "bash",
        "-c",
        "curl -s https://raw.githubusercontent.com/igoforth/RWAILib/AIServer/Server.py | python -"
    };

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

        // Create download jobs for binaries
        await DownloadFile(bashUrl, bashPath);
        await DownloadFile(pythonUrl, pythonPath);
        await DownloadFile(webZipUrl, webZipPath, extractPath: configPath);
        await DownloadFile(gitZipUrl, gitZipPath, extractPath: configPath);

        // Add the bin directory to the PATH
        Environment.SetEnvironmentVariable(
            "PATH",
            $"{binPath};{Environment.GetEnvironmentVariable("PATH")}"
        );

        // Run the bootstrap process
        BootstrapTool bt = new BootstrapTool();
        await bt.Bootstrap(cmdArgs, configPath);
    }

    public static async Task DownloadFile(
        string fileUrl,
        string destinationPath,
        string extractPath = ""
    )
    {
        // Ensure destinationPath is not directory
        if (Directory.Exists(destinationPath))
        {
            LogTool.Error("Destination path is a directory.");
            return;
        }
        // Ensure the destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

        try
        {
            using (HttpClient client = new HttpClient())
            {
                // Download the file
                using (var response = await client.GetAsync(fileUrl))
                {
                    response.EnsureSuccessStatusCode();
                    using (var ms = await response.Content.ReadAsStreamAsync())
                    {
                        using (
                            var fs = new FileStream(
                                destinationPath,
                                FileMode.Create,
                                FileAccess.Write
                            )
                        )
                        {
                            ms.CopyTo(fs);
                        }
                    }
                }

                // Extract the zip file
                LogTool.Message("Download completed successfully.");
            }
        }
        catch (Exception ex)
        {
            LogTool.Error($"An error occurred: {ex.Message}");
            return;
        }

        if (extractPath != "")
        {
            try
            {
                ZipFile.ExtractToDirectory(destinationPath, extractPath);
                LogTool.Message("Extraction completed successfully.");
            }
            catch (Exception ex)
            {
                LogTool.Error($"An error occurred: {ex.Message}");
                return;
            }
        }
    }

    public async Task Bootstrap(string[] cmdArgs, string workDir)
    {
        eventHandled = new TaskCompletionSource<bool>();

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = cmdArgs[0],
            Arguments = $"{cmdArgs[1]} \"{cmdArgs[2]}\"",
            WorkingDirectory = workDir,
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
